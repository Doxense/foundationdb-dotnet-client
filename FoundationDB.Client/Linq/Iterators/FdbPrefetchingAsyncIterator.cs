﻿#region BSD Licence
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
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Prefetches items from the inner sequence, before outputing them down the line.</summary>
	/// <typeparam name="TInput">Type the the items from the source sequence</typeparam>
	internal class FdbPrefetchingAsyncIterator<TInput> : FdbAsyncFilterIterator<TInput, TInput>
	{
		// This iterator can be used to already ask for the next few items, while they are being processed somewhere down the line of the query.
		// This can be usefull, when combined with Batching or Windowing, to maximize the throughput of db queries that read pages of results at a time.

		// ITERABLE

		private int m_prefetchCount;						// max number of items to prefetch

		// ITERATOR

		private Queue<TInput> m_buffer;						// buffer storing the items in the current window
		private Task<bool> m_nextTask;						// holds on to the last pending call to m_iterator.MoveNext() when our buffer is full

		/// <summary>Create a new batching iterator</summary>
		/// <param name="source">Source sequence of items that must be batched by waves</param>
		/// <param name="prefetchCount">Maximum size of a batch to return down the line</param>
		public FdbPrefetchingAsyncIterator(IFdbAsyncEnumerable<TInput> source, int prefetchCount)
			: base(source)
		{
			Contract.Requires(prefetchCount > 0);
			m_prefetchCount = prefetchCount;
		}

		protected override FdbAsyncIterator<TInput> Clone()
		{
			return new FdbPrefetchingAsyncIterator<TInput>(m_source, m_prefetchCount);
		}

		protected override void OnStarted(IFdbAsyncEnumerator<TInput> iterator)
		{
			// pre-allocate the buffer with the number of slot we expect to use
			m_buffer = new Queue<TInput>(m_prefetchCount);
		}

		protected override Task<bool> OnNextAsync(CancellationToken ct)
		{
			var buffer = m_buffer;
			if (buffer != null && buffer.Count > 0)
			{
				var nextTask = m_nextTask;
				if (nextTask == null || !m_nextTask.IsCompleted)
				{
					var current = buffer.Dequeue();
					return Publish(current) ? TaskHelpers.TrueTask : TaskHelpers.FalseTask;
				}
			}

			return PrefetchNextItemsAsync(ct);
		}

		protected virtual async Task<bool> PrefetchNextItemsAsync(CancellationToken ct)
		{
			// read items from the source until the next call to Inner.MoveNext() is not already complete, or we have filled our prefetch buffer, then returns the first item in the buffer.

			var t = Interlocked.Exchange(ref m_nextTask, null);
			if (t == null)
			{ // read the next item from the inner iterator
				if (m_innerHasCompleted) return Completed();
				t = m_iterator.MoveNext(ct);
			}

			// always wait for the first item (so that we have at least something in the batch)
			bool hasMore = await t.ConfigureAwait(false);

			// most db queries will read items by chunks, so there is a high chance the the next following calls to MoveNext() will already be completed
			// as long as this is the case, and that our buffer is not full, continue eating items. Stop only when we end up with a pending task.

			while (hasMore && !ct.IsCancellationRequested)
			{
				if (m_buffer == null) m_buffer = new Queue<TInput>(m_prefetchCount);
				m_buffer.Enqueue(m_iterator.Current);

				t = m_iterator.MoveNext(ct);
				if (m_buffer.Count >= m_prefetchCount || !t.IsCompleted)
				{ // save it for next time
					m_nextTask = t;
					break;
				}

				// we know the task is already completed, so we will immediately get the next result, or blow up if the inner iterator failed
				hasMore = t.GetAwaiter().GetResult();
				//note: if inner blows up, we won't send any previously read items down the line. This may change the behavior of queries with a .Take(N) that would have stopped before reading the (N+1)th item that would have failed.
			}
			ct.ThrowIfCancellationRequested();

			if (!hasMore)
			{
				MarkInnerAsCompleted();
				if (m_buffer == null || m_buffer.Count == 0)
				{ // that was the last batch!
					return Completed();
				}
			}

			var current = m_buffer.Dequeue();
			return Publish(current);
		}

		protected override void OnStopped()
		{
			m_buffer = null;

			// defuse the task, which should fail once we dispose the inner iterator below...
			var nextTask = Interlocked.Exchange(ref m_nextTask, null);
			if (nextTask != null) TaskHelpers.Observe(nextTask);
		}

	}
}
