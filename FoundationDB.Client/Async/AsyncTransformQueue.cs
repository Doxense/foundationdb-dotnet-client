#region BSD Licence
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

namespace Doxense.Async
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Implements an async queue that asynchronously transform items, outputing them in arrival order, while throttling the producer</summary>
	/// <typeparam name="TInput">Type of the input elements (from the inner async iterator)</typeparam>
	/// <typeparam name="TOutput">Type of the output elements (produced by an async lambda)</typeparam>
	public class AsyncTransformQueue<TInput, TOutput> : IAsyncBuffer<TInput, TOutput>
	{
		private readonly Func<TInput, CancellationToken, Task<TOutput>> m_transform;
		private readonly Queue<Task<Maybe<TOutput>>> m_queue = new Queue<Task<Maybe<TOutput>>>();
		private readonly object m_lock = new object();
		private readonly int m_capacity;
		private AsyncCancelableMutex m_blockedProducer;
		private AsyncCancelableMutex m_blockedConsumer;
		private bool m_done;
		private readonly TaskScheduler m_scheduler;

		public AsyncTransformQueue([NotNull] Func<TInput, CancellationToken, Task<TOutput>> transform, int capacity, TaskScheduler scheduler)
		{
			Contract.NotNull(transform, nameof(transform));
			if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero");

			m_transform = transform;
			m_capacity = capacity;
			m_scheduler = scheduler ?? TaskScheduler.Default;
		}

		#region IAsyncBuffer<TInput, TOutput>...

		/// <summary>Returns the current number of items in the queue</summary>
		public int Count
		{
			get
			{
				Debugger.NotifyOfCrossThreadDependency();
				lock (m_lock)
				{
					return m_queue.Count;
				}
			}
		}

		/// <summary>Returns the maximum capacity of the queue</summary>
		public int Capacity => m_capacity;

		/// <summary>Returns true if the producer is blocked (queue is full)</summary>
		public bool IsConsumerBlocked
		{
			get
			{
				Debugger.NotifyOfCrossThreadDependency();
				lock (m_lock)
				{
					return m_blockedConsumer?.Task.IsCompleted == true;
				}
			}
		}

		/// <summary>Returns true if the consumer is blocked (queue is empty)</summary>
		public bool IsProducerBlocked
		{
			get
			{
				Debugger.NotifyOfCrossThreadDependency();
				lock (m_lock)
				{
					return m_blockedProducer?.Task.IsCompleted == true;
				}
			}
		}

		public Task DrainAsync()
		{
			throw new NotImplementedException();
		}

		#endregion

		#region IAsyncTarget<TInput>...

		private static async Task<Maybe<TOutput>> ProcessItemHandler(object state)
		{
			try
			{
				var prms = (Tuple<AsyncTransformQueue<TInput, TOutput>, TInput, CancellationToken>)state;
				var task = prms.Item1.m_transform(prms.Item2, prms.Item3);
				if (!task.IsCompleted)
				{
					await task.ConfigureAwait(false);
				}
				return Maybe.FromTask<TOutput>(task);
			}
			catch (Exception e)
			{
				return Maybe.Error<TOutput>(ExceptionDispatchInfo.Capture(e));
			}
		}

		private static readonly Func<object, Task<Maybe<TOutput>>> s_processItemHandler = ProcessItemHandler;

		public async Task OnNextAsync(TInput value, CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				AsyncCancelableMutex waiter;
				lock (m_lock)
				{
					if (m_done) throw new InvalidOperationException("Cannot post more values after calling Complete()");

					if (m_queue.Count < m_capacity)
					{
						var t = Task.Factory.StartNew(
							s_processItemHandler,
							Tuple.Create(this, value, ct),
							ct,
							TaskCreationOptions.PreferFairness,
							m_scheduler
						).Unwrap();

						m_queue.Enqueue(t);

						var _ = t.ContinueWith((_t) =>
						{
							lock (m_lock)
							{
								// we should only wake up the consumers if we are the fist in the queue !
								if (m_queue.Count > 0 && m_queue.Peek() == _t)
								{
									WakeUpConsumer_NeedLocking();
								}
							}
						}, TaskContinuationOptions.ExecuteSynchronously);

						return;
					}

					// no luck, we need to wait for the queue to become non-full
					waiter = new AsyncCancelableMutex(ct);
					m_blockedProducer = waiter;
				}

				await waiter.Task.ConfigureAwait(false);
			}

			ct.ThrowIfCancellationRequested();
		}

		public void OnCompleted()
		{
			lock (m_lock)
			{
				if (m_done) throw new InvalidOperationException("OnCompleted() and OnError() can only be called once");
				m_done = true;
				m_queue.Enqueue(Maybe<TOutput>.EmptyTask);
				if (m_queue.Count == 1)
				{
					WakeUpConsumer_NeedLocking();
				}
			}
		}

		public void OnError(ExceptionDispatchInfo error)
		{
			lock(m_lock)
			{
				if (m_done) throw new InvalidOperationException("OnCompleted() and OnError() can only be called once");
				m_done = true;
				m_queue.Enqueue(Task.FromResult(Maybe.Error<TOutput>(error)));
				if (m_queue.Count == 1)
				{
					WakeUpConsumer_NeedLocking();
				}
			}
		}

		#endregion

		#region IAsyncBatchTarget<TInput>...

		public async Task OnNextBatchAsync([NotNull] TInput[] batch, CancellationToken cancellationToken)
		{
			Contract.NotNull(batch, nameof(batch));

			if (batch.Length == 0) return;

			if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

			//TODO: optimized version !
			foreach (var item in batch)
			{
				await OnNextAsync(item, cancellationToken).ConfigureAwait(false);
			}
		}

		#endregion

		#region IAsyncSource<TOutput>...

		public Task<Maybe<TOutput>> ReceiveAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			// if the first item in the queue is completed, we return it immediately (fast path)
			// if the first item in the queue is not yet completed, we need to wait for it (semi-fast path)
			// if the queue is empty, we first need to wait for an item to arrive, then wait for it

			Task<Maybe<TOutput>> task = null;
			Task waiter = null;

			lock(m_lock)
			{
				if (m_queue.Count > 0)
				{
					task = m_queue.Peek();
					Contract.Assert(task != null);

					if (task.IsCompleted)
					{
						m_queue.Dequeue();
						WakeUpProducer_NeedsLocking();
						return Maybe.Unwrap(task);
					}
				}
				else if (m_done)
				{ // something went wrong...
					return Maybe<TOutput>.EmptyTask;
				}
				else
				{
					waiter = WaitForNextItem_NeedsLocking(ct);
					Contract.Assert(waiter != null);
				}
			}


			if (task != null)
			{ // the next task is already started, we need to wait for it
				return ReceiveWhenDoneAsync(task, ct);
			}
			else
			{ // nothing schedule yet, slow code path will wait for something new to happen...
				return ReceiveSlowAsync(waiter, ct);
			}
		}

		private async Task<Maybe<TOutput>> ReceiveWhenDoneAsync(Task<Maybe<TOutput>> task, CancellationToken ct)
		{
			try
			{
				ct.ThrowIfCancellationRequested();
				//TODO: use the cancellation token !
				return await task.ConfigureAwait(false);
			}
			catch(Exception e)
			{
				return Maybe.Error<TOutput>(ExceptionDispatchInfo.Capture(e));
			}
			finally
			{
				lock (m_lock)
				{
					if (m_queue.Count > 0)
					{
						var _ = m_queue.Dequeue();
						WakeUpProducer_NeedsLocking();
					}
				}
			}
		}

		private async Task<Maybe<TOutput>> ReceiveSlowAsync(Task waiter, CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				await waiter.ConfigureAwait(false);

				Task<Maybe<TOutput>> task = null;
				lock (m_lock)
				{
					if (m_queue.Count > 0)
					{
						task = m_queue.Peek();
					}
					else if (m_done)
					{ // something went wrong?
						return Maybe.Nothing<TOutput>();
					}
				}

				if (task != null)
				{
					return await ReceiveWhenDoneAsync(task, ct).ConfigureAwait(false);
				}

				lock(m_lock)
				{
					// we need to wait again
					waiter = WaitForNextItem_NeedsLocking(ct);
					Contract.Assert(waiter != null);
				}

			}

			ct.ThrowIfCancellationRequested();
			return Maybe.Nothing<TOutput>();
		}

		#endregion

		#region IAsyncBatchSource<TOutput>...

		public Task<Maybe<TOutput>[]> ReceiveBatchAsync(int count, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var batch = new List<Maybe<TOutput>>();

			// consume everything that is already in the buffer
			lock(m_lock)
			{
				if (DrainItems_NeedsLocking(batch, count))
				{ // got some stuff, we need to wake up any locked writer on the way out !

					WakeUpProducer_NeedsLocking();
				}
			}

			if (batch.Count > 0)
			{ // got some things, return them
				return Task.FromResult(batch.ToArray());
			}

			// did not get anything, go through the slow code path
			return ReceiveBatchSlowAsync(batch, count, ct);
		}

		private async Task<Maybe<TOutput>[]> ReceiveBatchSlowAsync([NotNull] List<Maybe<TOutput>> batch, int count, CancellationToken ct)
		{
			// got nothing, wait for at least one
			while (batch.Count == 0)
			{
				Task waiter;
				lock(m_lock)
				{
					waiter = WaitForNextItem_NeedsLocking(ct);
					Contract.Assert(waiter != null);
				}

				await waiter.ConfigureAwait(false);

				// try to get all extra values that could have been added at the same time
				lock (m_lock)
				{
					if (DrainItems_NeedsLocking(batch, count))
					{
						WakeUpProducer_NeedsLocking();
					}

					if (m_done) break;
				}
			}

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			return batch.ToArray();
		}

		#endregion

		private bool DrainItems_NeedsLocking([NotNull] List<Maybe<TOutput>> buffer, int count)
		{
			// tries to return all completed tasks at the start of the queue

			bool gotAny = false;
			while (m_queue.Count > 0 && buffer.Count < count)
			{
				var task = m_queue.Peek();
				if (!task.IsCompleted) break; // not yet completed

				// remove it from the queue
				m_queue.Dequeue();

				var result = Maybe.FromTask(task);
				buffer.Add(result);
				if (!result.HasValue) break;
				gotAny = true;
			}

			return gotAny;
		}

		private Task WaitForNextItem_NeedsLocking(CancellationToken ct)
		{
			if (m_done) return Task.CompletedTask;

			Contract.Requires(m_blockedConsumer == null || m_blockedConsumer.Task.IsCompleted);

			var waiter = new AsyncCancelableMutex(ct);
			m_blockedConsumer = waiter;
			return waiter.Task;
		}

		private void WakeUpProducer_NeedsLocking()
		{
			var waiter = Interlocked.Exchange(ref m_blockedProducer, null);
			waiter?.Set(async: true);
		}

		private void WakeUpConsumer_NeedLocking()
		{
			var waiter = Interlocked.Exchange(ref m_blockedConsumer, null);
			waiter?.Set(async: true);
		}

	}

}
