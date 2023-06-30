#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Async
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq;
	using Doxense.Threading.Tasks;
	using Doxense.Tools;

	/// <summary>Helper methods for creating and manipulating async sequences.</summary>
	public static class AsyncHelpers
	{
		internal static readonly Action NoOpCompletion = () => { };
		internal static readonly Action<ExceptionDispatchInfo> NoOpError = (e) => { };
		internal static readonly Action<ExceptionDispatchInfo> RethrowError = (e) => { e.Throw(); };

		#region Targets...

		/// <summary>Create a new async target from a set of callbacks</summary>
		public static IAsyncTarget<T> CreateTarget<T>(
			Func<T, CancellationToken, Task> onNextAsync,
			Action onCompleted = null,
			Action<ExceptionDispatchInfo> onError = null
		)
		{
			return new AnonymousAsyncTarget<T>(onNextAsync, onCompleted, onError);
		}

		/// <summary>Create a new async target from a set of callbacks</summary>
		public static IAsyncTarget<T> CreateTarget<T>(
				Action<T, CancellationToken> onNext,
				Action onCompleted = null,
				Action<ExceptionDispatchInfo> onError = null
		)
		{
			return new AnonymousTarget<T>(onNext, onCompleted, onError);
		}

		/// <summary>Publish a new result on this async target, by correclty handling success, termination and failure</summary>
		public static Task Publish<T>(this IAsyncTarget<T> target, Maybe<T> result, CancellationToken ct)
		{
			Contract.Debug.Requires(target != null);

			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			if (result.HasValue)
			{ // we have the next value
				return target.OnNextAsync(result.Value, ct);
			}

			if (result.Failed)
			{ // we have failed
				target.OnError(result.CapturedError);
				return Task.CompletedTask;
			}

			// this is the end of the stream
			target.OnCompleted();
			return Task.CompletedTask;
		}

		/// <summary>Wrapper class for use with async lambda callbacks</summary>
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

			public Task OnNextAsync(T value, CancellationToken ct)
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

		/// <summary>Wrapper class for use with non-async lambda callbacks</summary>
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
				Contract.NotNull(onNext);

				m_onNext = onNext;
				m_onCompleted = onCompleted;
				m_onError = onError;
			}

			public Task OnNextAsync(T value, CancellationToken ct)
			{
				if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
				try
				{
					m_onNext(value, ct);
					return Task.CompletedTask;
				}
				catch (Exception e)
				{
					return Task.FromException(e);
				}
			}

			public void OnCompleted()
			{
				m_onCompleted?.Invoke();
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

		/// <summary>Consumes all the elements of the source, and publish them to the target, one by one and in order</summary>
		/// <param name="source">Source that produces elements asynchronously</param>
		/// <param name="target">Target that consumes elements asynchronously</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that completes when all the elements of the source have been published to the target, or fails if on the first error, or the token is cancelled unexpectedly</returns>
		/// <remarks>The pump will only read one element at a time, and wait for it to be published to the target, before reading the next element.</remarks>
		public static async Task PumpToAsync<T>(this IAsyncSource<T> source, IAsyncTarget<T> target, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			bool notifiedCompletion = false;
			bool notifiedError = false;

			try
			{
				//LogPump("Starting pump");

				while (!ct.IsCancellationRequested)
				{
					//LogPump("Waiting for next");

					var current = await source.ReceiveAsync(ct).ConfigureAwait(false);

					//LogPump("Received " + (current.HasValue ? "value" : current.Failed ? "error" : "completion") + ", publishing... " + current);
					if (ct.IsCancellationRequested)
					{
						// REVIEW: should we notify the target?
						// REVIEW: if the item is IDisposble, who will clean up?
						break;
					}

					// push the data/error/completion on to the target, which will triage and update its state accordingly
					await target.Publish(current, ct).ConfigureAwait(false);

					if (current.Failed)
					{ // bounce the error back to the caller
					  //REVIEW: SHOULD WE? We poush the error to the target, and the SAME error to the caller... who should be responsible for handling it?
					  // => target should know about the error (to cancel something)
					  // => caller should maybe also know that the pump failed unexpectedly....
						notifiedError = true;
						current.ThrowForNonSuccess(); // throws an exception right here
						return; // should not be reached
					}
					else if (current.IsEmpty)
					{ // the source has completed, stop the pump
					  //LogPump("Completed");
						notifiedCompletion = true;
						return;
					}
				}

				// notify cancellation if it happend while we were pumping
				if (ct.IsCancellationRequested)
				{
					//LogPump("We were cancelled!");
					throw new OperationCanceledException(ct);
				}
			}
			catch (Exception e)
			{
				//LogPump("Failed: " + e);

				if (!notifiedCompletion && !notifiedError)
				{ // notify the target that we crashed while fetching the next
					try
					{
						//LogPump("Push error down to target: " + e.Message);
						target.OnError(ExceptionDispatchInfo.Capture(e));
						notifiedError = true;
					}
					catch (Exception x) when (!x.IsFatalError())
					{
						//LogPump("Failed to notify target of error: " + x.Message);
					}
				}

				throw;
			}
			finally
			{
				if (!notifiedCompletion)
				{ // we must be sure to complete the target if we haven't done so yet!
					//LogPump("Notify target of completion due to unexpected conditions");
					target.OnCompleted();
				}
				//LogPump("Stopped pump");
			}
		}

		/// <summary>Pump the content of a source into a list</summary>
		public static async Task<List<T>> PumpToListAsync<T>(this IAsyncSource<T> source, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var buffer = new Buffer<T>();

			var target = CreateTarget<T>(
				(x, _) => buffer.Add(x)
			);

			await PumpToAsync<T>(source, target, ct).ConfigureAwait(false);

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

		public static AsyncTransform<TInput, TOutput> CreateAsyncTransform<TInput, TOutput>(Func<TInput, CancellationToken, Task<TOutput>> transform, IAsyncTarget<Task<TOutput>> target, TaskScheduler scheduler = null)
		{
			return new AsyncTransform<TInput, TOutput>(transform, target, scheduler);
		}

		public static async Task<List<TOutput>> TransformToListAsync<TInput, TOutput>(IAsyncSource<TInput> source, Func<TInput, CancellationToken, Task<TOutput>> transform, CancellationToken ct, int? maxConcurrency = null, TaskScheduler scheduler = null)
		{
			ct.ThrowIfCancellationRequested();

			using (var queue = CreateOrderPreservingAsyncBuffer<TOutput>(maxConcurrency ?? 32))
			{
				using (var pipe = CreateAsyncTransform<TInput, TOutput>(transform, queue, scheduler))
				{
					// start the output pump
					var output = PumpToListAsync(queue, ct);

					// start the intput pump
					var input = PumpToAsync(source, pipe, ct);

					await Task.WhenAll(input, output).ConfigureAwait(false);

					return output.Result;
				}
			}
		}

		#endregion

	}
}
