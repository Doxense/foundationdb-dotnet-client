#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

//#define FULL_DEBUG

namespace Doxense.Async
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Buffer that holds a fixed number of items and can rate-limit the producer</summary>
	/// <typeparam name="TInput"></typeparam>
	/// <typeparam name="TOutput"></typeparam>
	public class AsyncBuffer<TInput, TOutput> : AsyncProducerConsumerQueue<TInput>, IAsyncSource<TOutput>
	{
		#region Private Members...

		/// <summary>Transformation applied on the values</summary>
		private readonly Func<TInput, TOutput> m_transform;

		/// <summary>Queue that holds items produced but not yet consumed</summary>
		/// <remarks>The queue can sometime go over the limit because the Complete/Error message are added without locking</remarks>
		private readonly Queue<Maybe<TInput>> m_queue = new Queue<Maybe<TInput>>();

		#endregion

		#region Constructors...

		public AsyncBuffer(Func<TInput, TOutput> transform, int capacity)
			: base(capacity)
		{
			Contract.NotNull(transform);

			m_transform = transform;
		}

		#endregion

		#region IAsyncTarget<T>...

		public override Task OnNextAsync(TInput value, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			LogProducer("Received new value");

			Task wait;
			lock (m_lock)
			{
				if (m_done) return Task.FromException<object>(new InvalidOperationException("Cannot send any more values because this buffer has already completed"));

				if (m_queue.Count < m_capacity)
				{ // quick path
					Enqueue_NeedsLocking(Maybe.Return(value));
					return Task.CompletedTask;
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
					m_queue.Enqueue(Maybe.Nothing<TInput>());
					WakeUpBlockedConsumer_NeedsLocking();
				}
			}
		}

		public override void OnError(ExceptionDispatchInfo error)
		{
			lock (m_lock)
			{
				if (!m_done)
				{
					LogProducer("Error received: " + error.SourceException.Message);
					m_queue.Enqueue(Maybe.Error<TInput>(error));
					WakeUpBlockedConsumer_NeedsLocking();
				}
			}
		}

		private void Enqueue_NeedsLocking(Maybe<TInput> value)
		{
			m_queue.Enqueue(value);

			if (m_queue.Count == 1)
			{
				WakeUpBlockedConsumer_NeedsLocking();
			}
		}

		private async Task WaitForNextFreeSlotThenEnqueueAsync(TInput value, Task wait, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			await wait.ConfigureAwait(false);

			LogProducer("Wake up because one slot got freed");

			lock (m_lock)
			{
				Contract.Debug.Assert(m_queue.Count < m_capacity);
				Enqueue_NeedsLocking(Maybe.Return(value));
			}
		}

		#endregion

		#region IAsyncSource<R>...

		public Task<Maybe<TOutput>> ReceiveAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Maybe<TOutput>>(ct);

			LogConsumer("Looking for next value...");

			Task wait = null;
			Maybe<TInput> item;
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
					item = Maybe.Nothing<TInput>();
				}
				else
				{
					wait = MarkConsumerAsBlocked_NeedsLocking(ct);
					item = default(Maybe<TInput>); // needed to please the compiler
				}
			}

			if (wait != null)
			{
				return WaitForNextItemAsync(wait, ct);
			}

			return Task.FromResult(ProcessResult(item));
		}

		private Maybe<TOutput> ProcessResult(Maybe<TInput> item)
		{
			if (item.IsEmpty)
			{ // that was the last one !
				m_receivedLast = true;
				LogConsumer("Received last item");
				return Maybe.Nothing<TOutput>();
			}

			LogConsumer("Applying transform on item");
			return Maybe.Apply<TInput, TOutput>(item, m_transform);
		}

		private async Task<Maybe<TOutput>> WaitForNextItemAsync(Task wait, CancellationToken ct)
		{
			await wait.ConfigureAwait(false);

			LogConsumer("Wake up because one item arrived");

			Maybe<TInput> item;
			lock(m_lock)
			{
				ct.ThrowIfCancellationRequested();

				Contract.Debug.Assert(m_queue.Count > 0);
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
