#region Copyright Doxense SAS 2013-2016
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
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Capacity={m_capacity}, ReceivedLast={m_receivedLast}, Done={m_done}")]
	public abstract class AsyncProducerConsumerQueue<T> : IAsyncTarget<T>, IDisposable
	{
		/// <summary>Lock used to secure the global state</summary>
		protected readonly object m_lock = new object();

		/// <summary>Maximum capacity of the queue (number of values not yet consumed)</summary>
		protected int m_capacity;

		/// <summary>If true, the last item has been sent by the source</summary>
		protected bool m_done;

		/// <summary>If true, the last item has been received by the target</summary>
		protected bool m_receivedLast;

		/// <summary>Mutex signaling that the producer is blocked on a full queue</summary>
		protected AsyncCancelableMutex m_producerLock = AsyncCancelableMutex.AlreadyDone;

		/// <summary>Mutex signaling that the consumer is blocked on an empty queue</summary>
		protected AsyncCancelableMutex m_consumerLock = AsyncCancelableMutex.AlreadyDone;

		protected AsyncProducerConsumerQueue(int capacity)
		{
			if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero");

			m_capacity = capacity;
		}

		public abstract Task OnNextAsync(T value, CancellationToken ct);

		public abstract void OnCompleted();

		public abstract void OnError(ExceptionDispatchInfo error);

		/// <summary>Delcare the producer as beeing blocked on a full queue</summary>
		/// <param name="ct"></param>
		/// <returns></returns>
		protected Task MarkProducerAsBlocked_NeedsLocking(CancellationToken ct)
		{
			if (ct.IsCancellationRequested)
			{
				return Task.FromCanceled(ct);
			}
			if (m_producerLock.IsCompleted)
			{
				m_producerLock = new AsyncCancelableMutex(ct);
			}
			LogProducer("blocked on full");
			return m_producerLock.Task;
		}

		/// <summary>Wake up the producer if it is blocked</summary>
		protected void WakeUpBlockedProducer_NeedsLocking()
		{
			if (m_producerLock.Set(async: true))
			{
				LogConsumer("Woke up blocked producer");
			}
		}

		/// <summary>Declare the consumer as beeing blocked on an empty queue</summary>
		/// <param name="ct"></param>
		/// <returns></returns>
		protected Task MarkConsumerAsBlocked_NeedsLocking(CancellationToken ct)
		{
			if (ct.IsCancellationRequested)
			{
				return Task.FromCanceled(ct);
			}
			if (m_consumerLock.IsCompleted)
			{
				m_consumerLock = new AsyncCancelableMutex(ct);
			}
			LogConsumer("blocked on empty");
			return m_consumerLock.Task;
		}

		/// <summary>Wake up the consumer if it is blocked</summary>
		protected void WakeUpBlockedConsumer_NeedsLocking()
		{
			if (m_consumerLock.Set(async: true))
			{
				LogProducer("Woke up blocked consumer");
			}
			else
			{
				LogProducer("Consumer was already unblocked?");
			}
		}

		protected void EnsureNotDone()
		{
			if (m_done) throw new InvalidOperationException("Cannot perform this operation on an already completed buffer");
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected abstract void Dispose(bool disposing);

		#region Debug Logging...

		[Conditional("FULL_DEBUG")]
		protected void LogProducer(string msg, [CallerMemberName] string caller = null)
		{
#if FULL_DEBUG
			Console.WriteLine("@@@ [producer#{0}] {1} [{2}]", Thread.CurrentThread.ManagedThreadId, msg, caller);
#endif
		}

		[Conditional("FULL_DEBUG")]
		protected void LogConsumer(string msg, [CallerMemberName] string caller = null)
		{
#if FULL_DEBUG
			Console.WriteLine("@@@ [consumer#{0}] {1} [{2}]", Thread.CurrentThread.ManagedThreadId, msg, caller);
#endif
		}

		#endregion

	}

}
