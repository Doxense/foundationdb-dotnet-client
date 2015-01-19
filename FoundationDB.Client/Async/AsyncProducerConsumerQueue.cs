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

//#define FULL_DEBUG

namespace FoundationDB.Async
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
			if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity", "Capacity must be greater than zero");

			m_capacity = capacity;
		}

		public abstract Task OnNextAsync(T value, CancellationToken cancellationToken);

		public abstract void OnCompleted();

#if NET_4_0
		public abstract void OnError(Exception error);
#else
		public abstract void OnError(ExceptionDispatchInfo error);
#endif

		/// <summary>Delcare the producer as beeing blocked on a full queue</summary>
		/// <param name="ct"></param>
		/// <returns></returns>
		protected Task MarkProducerAsBlocked_NeedsLocking(CancellationToken ct)
		{
			if (ct.IsCancellationRequested)
			{
				return TaskHelpers.FromCancellation<object>(ct);
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
				return TaskHelpers.FromCancellation<object>(ct);
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
			Console.WriteLine("@@@ [producer#{0}] {1} [{2}]", Thread.CurrentThread.ManagedThreadId, msg, caller);
		}

		[Conditional("FULL_DEBUG")]
		protected void LogConsumer(string msg, [CallerMemberName] string caller = null)
		{
			Console.WriteLine("@@@ [consumer#{0}] {1} [{2}]", Thread.CurrentThread.ManagedThreadId, msg, caller);
		}

		#endregion

	}

}
