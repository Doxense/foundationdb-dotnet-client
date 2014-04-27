#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Helper methods for creating and manipulating async sequences.
	/// </summary>
	public static class AsyncHelpers
	{
		internal static readonly Action NoOpCompletion = () => { };
		internal static readonly Action<ExceptionDispatchInfo> NoOpError = (e) => { };
		internal static readonly Action<ExceptionDispatchInfo> RethrowError = (e) => { e.Throw(); };

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
				Action onCompleted = null,
				Action<ExceptionDispatchInfo> onError = null
		)
		{
			return new AnonymousTarget<T>(onNext, onCompleted, onError);
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
				target.OnError(result.CapturedError);
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

			public Task OnNextAsync(T value, CancellationToken cancellationToken)
			{
				return m_onNextAsync(value, cancellationToken);
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
				if (onNext == null) throw new ArgumentNullException("onNext");

				m_onNext = onNext;
				m_onCompleted = onCompleted;
				m_onError = onError;
			}

			public Task OnNextAsync(T value, CancellationToken cancellationToken)
			{
				return TaskHelpers.Inline(m_onNext, value, cancellationToken, cancellationToken);
			}

			public void OnCompleted()
			{
				if (m_onCompleted != null)
				{
					m_onCompleted();
				}
			}

			public void OnError(ExceptionDispatchInfo error)
			{
				if (m_onError != null)
					m_onError(error);
				else
					error.Throw();
			}
		}

		#endregion

		#region Pumps...

		public static async Task PumpToAsync<T>(this IAsyncSource<T> source, IAsyncTarget<T> target, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

			using (var pump = new AsyncPump<T>(source, target))
			{
				await pump.PumpAsync(stopOnFirstError: true, cancellationToken: cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>Pump the content of a source into a list</summary>
		public static async Task<List<T>> PumpToListAsync<T>(this IAsyncSource<T> source, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

			var buffer = new FoundationDB.Linq.FdbAsyncEnumerable.Buffer<T>();

			var target = CreateTarget<T>(
				(x, _) => buffer.Add(x)
			);

			await PumpToAsync<T>(source, target, cancellationToken).ConfigureAwait(false);

			return buffer.ToList();
		}


		#endregion

		#region Buffers...

		public static AsyncTaskBuffer<T> CreateOrderPreservingAsyncBuffer<T>(int capacity)
		{
			return new AsyncTaskBuffer<T>(AsyncOrderingMode.ArrivalOrder, capacity);
		}

		public static AsyncTaskBuffer<T> CreateUnorderedAsyncBuffer<T>(int capacity)
		{
			return new AsyncTaskBuffer<T>(AsyncOrderingMode.CompletionOrder, capacity);
		}

		#endregion

		#region Transforms...

		public static AsyncTransform<T, R> CreateAsyncTransform<T, R>(Func<T, CancellationToken, Task<R>> transform, IAsyncTarget<Task<R>> target, TaskScheduler scheduler = null)
		{
			return new AsyncTransform<T, R>(transform, target, scheduler);
		}

		public static async Task<List<R>> TransformToListAsync<T, R>(IAsyncSource<T> source, Func<T, CancellationToken, Task<R>> transform, CancellationToken cancellationToken, int? maxConcurrency = null, TaskScheduler scheduler = null)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using (var queue = CreateOrderPreservingAsyncBuffer<R>(maxConcurrency ?? 32))
			{
				using (var pipe = CreateAsyncTransform<T, R>(transform, queue, scheduler))
				{
					// start the output pump
					var output = PumpToListAsync(queue, cancellationToken);

					// start the intput pump
					var input = PumpToAsync(source, pipe, cancellationToken);

					await Task.WhenAll(input, output).ConfigureAwait(false);

					return output.Result;
				}
			}
		}

		#endregion

	}
}
