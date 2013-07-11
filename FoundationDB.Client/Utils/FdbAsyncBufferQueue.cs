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

namespace FoundationDB.Client.Utils
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;

	internal class FdbAsyncBufferQueue<T> : IFdbAsyncBuffer<T>
	{
		private readonly Queue<Maybe<T>> m_queue = new Queue<Maybe<T>>();
		private readonly object m_lock = new object();
		private readonly int m_capacity;
		private AsyncCancellableMutex m_blockedProducer;
		private List<AsyncCancellableMutex> m_blockedConsumers = new List<AsyncCancellableMutex>();
		private bool m_done;

		public FdbAsyncBufferQueue(int capacity)
		{
			if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity", "Capacity must be greater than zero");

			m_capacity = capacity;
		}

		public int Count
		{
			get { return m_queue.Count; }
		}

		public int Capacity
		{
			get { return m_capacity; }
		}

		public Task OnNextAsync(T value, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			//Console.WriteLine("# producing " + value);

			AsyncCancellableMutex waiter;
			lock(m_lock)
			{
				if (m_done) throw new InvalidOperationException("Cannot post more values after calling Complete()");

				//Console.WriteLine("# the queue already has " + m_queue.Count + " element(s) with " + m_blockedConsumers.Count + " blocked consumers");

				if (m_queue.Count < m_capacity)
				{
					//Console.WriteLine("# pushing neew value :)");

					m_queue.Enqueue(new Maybe<T>(value));

					WakeUpConsumers_NeedLocking();

					return TaskHelpers.CompletedTask;
				}

				//Console.WriteLine("# seems full, we need to wait :(");

				// no luck, we need to wait for the queue to become non-full
				waiter = new AsyncCancellableMutex(ct);
				m_blockedProducer = waiter;
			}

			return waiter.Task;
		}

		public async Task OnNextBatchAsync(T[] batch, CancellationToken ct)
		{
			if (batch == null) throw new ArgumentNullException("batch");

			if (batch.Length == 0) return;

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			//TODO: optimized version !
			foreach(var item in batch)
			{
				await OnNextAsync(item, ct).ConfigureAwait(false);
			}
		}

		public void OnCompleted()
		{
			lock (m_lock)
			{
				if (m_done) throw new InvalidOperationException("Complete() can only be called once");
				m_done = true;
				m_queue.Enqueue(Maybe<T>.Empty);
				WakeUpConsumers_NeedLocking();
			}
		}


		public void OnError(Exception error)
		{
			lock(m_lock)
			{
				m_queue.Enqueue(Maybe<T>.FromError(error));
			}
		}

#if !NET_4_0
		public void OnError(ExceptionDispatchInfo error)
		{
			lock(m_lock)
			{
				m_queue.Enqueue(Maybe<T>.FromError(error));
			}
		}
#endif

		public Task DrainAsync()
		{
			throw new NotImplementedException();
		}

		private bool DrainItems_NeedsLocking(List<Maybe<T>> buffer, int count)
		{
			bool gotAny = false;
			while (m_queue.Count > 0 && buffer.Count < count)
			{
				var value = m_queue.Dequeue();
				buffer.Add(value);
				if (!value.HasValue) break;
				gotAny = true;
			}

			return gotAny;
		}

		private Task WaitForNextItem_NeedsLocking(CancellationToken ct)
		{
			if (m_done) return TaskHelpers.CompletedTask;

			var waiter = new AsyncCancellableMutex(ct);
			m_blockedConsumers.Add(waiter);
			return waiter.Task;
		}

		private void WakeUpProducer_NeedsLocking()
		{
			//Console.WriteLine(": should we wake up the producer?");
			var waiter = Interlocked.Exchange(ref m_blockedProducer, null);
			if (waiter != null)
			{
				//Console.WriteLine(": waking up producer!");
				waiter.Set(async: true);
			}
		}

		private void WakeUpConsumers_NeedLocking()
		{
			//Console.WriteLine("# should we wake up the consumers ?");
			if (m_blockedConsumers.Count > 0)
			{
				//Console.WriteLine("# waking up " + m_blockedConsumers.Count + " consumers");
				var consumers = m_blockedConsumers.ToArray();
				m_blockedConsumers.Clear();
				foreach(var consumer in consumers)
				{
					consumer.Set(async: true);
				}
			}
		}

		public Task<Maybe<T>> ReceiveAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			// either there are already items in the queue, in which case we return immediately,
			// or if the queue is empty, then we will have to wait 

			Task waiter;
			lock(m_lock)
			{
				//Console.WriteLine(": The queue has " + m_queue.Count + " element(s) and a " + ((m_blockedProducer == null || m_blockedProducer.Task.IsCompleted) ? "non-blocked producer" : "blocked producer"));

				if (m_queue.Count > 0)
				{
					//Console.WriteLine(": Taking the first one :)");

					WakeUpProducer_NeedsLocking();

					return Task.FromResult(m_queue.Dequeue());
				}

				//Console.WriteLine(": The queue is empty, will need to wait :(");
				//if (m_blockedProducer != null && m_blockedProducer.Task.IsCompleted) Console.WriteLine(": uhoh, producer is blocked ???");

				waiter = WaitForNextItem_NeedsLocking(ct);
				Contract.Assert(waiter != null);
			}

			// slow code path
			return ReceiveSlowAsync(waiter, ct);
		}

		private async Task<Maybe<T>> ReceiveSlowAsync(Task waiter, CancellationToken ct)
		{
			while (true)
			{
				//Console.WriteLine(": Waiting for one slot to become free...");
				await waiter.ConfigureAwait(false);
				//Console.WriteLine(": Got something for me ?");

				lock (m_lock)
				{
					if (m_queue.Count > 0)
					{
						//Console.WriteLine(": yay !");

						WakeUpProducer_NeedsLocking();

						return m_queue.Dequeue();
					}
	
					// someone else took our item !
					//Console.WriteLine(": nay :(");
					waiter = WaitForNextItem_NeedsLocking(ct);
					Contract.Assert(waiter != null);
				}

				//Console.WriteLine(": got cancelled :(");
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			}
		}

		public Task<Maybe<T>[]> ReceiveBatchAsync(int count, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var batch = new List<Maybe<T>>();

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

		private async Task<Maybe<T>[]> ReceiveBatchSlowAsync(List<Maybe<T>> batch, int count, CancellationToken ct)
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
	}

}
