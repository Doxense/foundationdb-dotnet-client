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

namespace FoundationDB.Async
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;


	public static class AsyncHelpers
	{
		#region Targets...

		public static IAsyncTarget<T> CreateTarget<T>(
			Func<T, CancellationToken, Task> onNextAsync,
			Action onCompleted,
			Action<ExceptionDispatchInfo> onError
		)
		{
			return new AnonymousAsyncTarget<T>(onNextAsync, onCompleted, onError);
		}

		public static IAsyncTarget<T> CreateTarget<T>(
				Action<T, CancellationToken> onNext,
				Action onCompleted,
				Action<ExceptionDispatchInfo> onError
		)
		{
			return new AnonymousTarget<T>(onNext, onCompleted, onError);
		}

		public static void OnError<T>(this IAsyncTarget<T> target, Exception error)
		{
			target.OnError(ExceptionDispatchInfo.Capture(error));
		}

		public static Task Publish<T>(this IAsyncTarget<T> target, Maybe<T> result, CancellationToken ct)
		{
			Contract.Requires(target != null);

			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(ct);

			if (result.HasValue)
			{
				return target.OnNextAsync(result.Value, ct);
			}
			else if (result.HasFailed)
			{
				target.OnError(result.Error);
				return TaskHelpers.CompletedTask;
			}
			else
			{
				target.OnCompleted();
				return TaskHelpers.CompletedTask;
			}
		}

		internal sealed class AnonymousAsyncTarget<T> : IAsyncTarget<T>
		{

			private readonly Func<T, CancellationToken, Task> m_onNextAsync;

			private readonly Action m_onCompleted;

			private readonly Action<ExceptionDispatchInfo> m_onError;

			public AnonymousAsyncTarget(
				Func<T, CancellationToken, Task> onNextAsync,
				Action onCompleted,
				Action<ExceptionDispatchInfo> onError
			)
			{
				m_onNextAsync = onNextAsync;
				m_onCompleted = onCompleted;
				m_onError = onError;
			}

			public Task OnNextAsync(T value, CancellationToken ct = default(CancellationToken))
			{
				return m_onNextAsync(value, ct);
			}

			public void OnCompleted()
			{
				m_onCompleted();
			}

			public void OnError(ExceptionDispatchInfo error)
			{
				m_onError(error);
			}
		}

		internal sealed class AnonymousTarget<T> : IAsyncTarget<T>
		{

			private readonly Action<T, CancellationToken> m_onNext;

			private readonly Action m_onCompleted;

			private readonly Action<ExceptionDispatchInfo> m_onError;

			public AnonymousTarget(
				Action<T, CancellationToken> onNext,
				Action onCompleted,
				Action<ExceptionDispatchInfo> onError
			)
			{
				m_onNext = onNext;
				m_onCompleted = onCompleted;
				m_onError = onError;
			}

			public Task OnNextAsync(T value, CancellationToken ct = default(CancellationToken))
			{
				return TaskHelpers.Inline(m_onNext, value, ct, ct);
			}

			public void OnCompleted()
			{
				m_onCompleted();
			}

			public void OnError(ExceptionDispatchInfo error)
			{
				m_onError(error);
			}
		}

		#endregion

		#region Pumps...

		public static async Task PumpToAsync<T>(this IAsyncSource<T> source, IAsyncTarget<T> target, CancellationToken ct = default(CancellationToken))
		{
			using (var pump = new AsyncPump<T>(source, target))
			{
				await pump.PumpAsync(ct);
			}
		}

		#endregion

		#region Buffers...

		public static AsyncTaskBuffer<T> CreateOrderPreservingAsyncBuffer<T>(int capacity)
		{
			return new AsyncTaskBuffer<T>(AsyncTaskBuffer<T>.OrderingMode.ArrivalOrder, capacity);
		}

		public static AsyncTaskBuffer<T> CreateUnorderedAsyncBuffer<T>(int capacity)
		{
			return new AsyncTaskBuffer<T>(AsyncTaskBuffer<T>.OrderingMode.CompletionOrder, capacity);
		}

		#endregion

	}
}
