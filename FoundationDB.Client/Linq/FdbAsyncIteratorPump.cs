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

namespace FoundationDB.Linq
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Pump that calls MoveNext on an iterator and tries to publish the values in a Producer/Consumer queue</summary>
	/// <typeparam name="TInput"></typeparam>
	internal class FdbAsyncIteratorPump<TInput>
	{

		private readonly IFdbAsyncEnumerator<TInput> m_iterator;
		private readonly IFdbAsyncBuffer<TInput> m_queue;

		public FdbAsyncIteratorPump(
			IFdbAsyncEnumerator<TInput> iterator,
			IFdbAsyncBuffer<TInput> queue
		)
		{
			Contract.Requires(iterator != null);
			Contract.Requires(queue != null && queue.Capacity > 0);

			m_iterator = iterator;
			m_queue = queue;
		}

		public async Task PumpAsync(CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					if (!(await m_iterator.MoveNext(ct).ConfigureAwait(false)))
					{
						m_queue.OnCompleted();
						return;
					}

					await m_queue.OnNextAsync(m_iterator.Current, ct).ConfigureAwait(false);
				}

				// push the cancellation on the queue
				m_queue.OnError(new OperationCanceledException(ct));

			}
			catch (Exception e)
			{
				// push the error on the queue
				m_queue.OnError(e);
				return;
			}
		}

	}

}
