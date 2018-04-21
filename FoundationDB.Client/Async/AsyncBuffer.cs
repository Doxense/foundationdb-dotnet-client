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

//#define FULL_DEBUG

namespace FoundationDB.Async
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Buffer that holds a fixed number of items and can rate-limit the producer</summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="R"></typeparam>
	public class AsyncBuffer<T, R> : AsyncProducerConsumerQueue<T>, IAsyncSource<R>
	{
		#region Private Members...

		/// <summary>Transformation applied on the values</summary>
		private readonly Func<T, R> m_transform;

		/// <summary>Queue that holds items produced but not yet consumed</summary>
		/// <remarks>The queue can sometime go over the limit because the Complete/Error message are added without locking</remarks>
		private readonly Queue<Maybe<T>> m_queue = new Queue<Maybe<T>>();

		#endregion

		#region Constructors...

		public AsyncBuffer([NotNull] Func<T, R> transform, int capacity)
			: base(capacity)
		{
			if (transform == null) throw new ArgumentNullException("transform");

			m_transform = transform;
		}

		#endregion

		#region IFdbAsyncTarget<T>...

		public override Task OnNextAsync(T value, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(ct);

			LogProducer("Received new value");

			Task wait;
			lock (m_lock)
			{
				if (m_done) return TaskHelpers.FromException<object>(new InvalidOperationException("Cannot send any more values because this buffer has already completed"));

				if (m_queue.Count < m_capacity)
				{ // quick path
					Enqueue_NeedsLocking(Maybe.Return(value));
					return TaskHelpers.CompletedTask;
				}

				// we are blocked, we will need to wait !
				wait = MarkProducerAsBlocked_NeedsLocking(ct);
			}

			// slow path
			return WaitForNextFreeSlotThenEnqueueAsync(value, wait, ct);
		}

		public override void OnCompleted()
		{
			lock (m_lock)
			{
				if (!m_done)
				{
					LogProducer("Completion received");
					m_done = true;
					m_queue.Enqueue(Maybe.Nothing<T>());
					WakeUpBlockedConsumer_NeedsLocking();
				}
			}
		}

#if NET_4_0
		public override void OnError(Exception error)
		{
			lock (m_lock)
			{
				if (!m_done)
				{
					LogProducer("Error received: " + error.Message);
					m_queue.Enqueue(Maybe.Error<T>(error));
					WakeUpBlockedConsumer_NeedsLocking();
				}
			}
		}
#else
		public override void OnError(ExceptionDispatchInfo error)
		{
			lock (m_lock)
			{
				if (!m_done)
				{
					LogProducer("Error received: " + error.SourceException.Message);
					m_queue.Enqueue(Maybe.Error<T>(error));
					WakeUpBlockedConsumer_NeedsLocking();
				}
			}
		}
#endif

		private void Enqueue_NeedsLocking(Maybe<T> value)
		{
			m_queue.Enqueue(value);

			if (m_queue.Count == 1)
			{
				WakeUpBlockedConsumer_NeedsLocking();
			}
		}

		private async Task WaitForNextFreeSlotThenEnqueueAsync(T value, Task wait, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			await wait.ConfigureAwait(false);

			LogProducer("Wake up because one slot got freed");

			lock (m_lock)
			{
				Contract.Assert(m_queue.Count < m_capacity);
				Enqueue_NeedsLocking(Maybe.Return(value));
			}
		}

		#endregion

		#region IFdbAsyncSource<R>...

		public Task<Maybe<R>> ReceiveAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<Maybe<R>>(ct);

			LogConsumer("Looking for next value...");

			Task wait = null;
			Maybe<T> item;
			lock (m_lock)
			{
				if (m_queue.Count > 0)
				{
					item = m_queue.Dequeue();
					LogConsumer("There was at least one item in the queue");
					WakeUpBlockedProducer_NeedsLocking();
				}
				else if (m_done)
				{
					LogConsumer("The queue was complete");
					item = Maybe.Nothing<T>();
				}
				else
				{
					wait = MarkConsumerAsBlocked_NeedsLocking(ct);
					item = default(Maybe<T>); // needed to please the compiler
				}
			}

			if (wait != null)
			{
				return WaitForNextItemAsync(wait, ct);
			}

			return Task.FromResult(ProcessResult(item));
		}

		private Maybe<R> ProcessResult(Maybe<T> item)
		{
			if (item.IsEmpty)
			{ // that was the last one !
				m_receivedLast = true;
				LogConsumer("Received last item");
				return Maybe.Nothing<R>();
			}

			LogConsumer("Applying transform on item");
			return Maybe.Apply<T, R>(item, m_transform);
		}

		private async Task<Maybe<R>> WaitForNextItemAsync(Task wait, CancellationToken ct)
		{
			await wait.ConfigureAwait(false);

			LogConsumer("Wake up because one item arrived");

			Maybe<T> item;
			lock(m_lock)
			{
				ct.ThrowIfCancellationRequested();

				Contract.Assert(m_queue.Count > 0);
				item = m_queue.Dequeue();
				WakeUpBlockedProducer_NeedsLocking();
			}

			return ProcessResult(item);
		}

		#endregion

		#region IDisposable...

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				lock (m_lock)
				{
					if (!m_done)
					{
						m_done = true;
						m_producerLock.Abort();
						m_consumerLock.Abort();
					}
				}
			}
		}

		#endregion
	}

}
