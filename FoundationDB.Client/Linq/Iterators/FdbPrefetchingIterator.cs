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

namespace FoundationDB.Client
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Iterator that prefetchs items from an inner sequence.</summary>
	/// <typeparam name="TInput">Type the the items from the source sequence</typeparam>
	internal class FdbPrefetchingIterator<TInput> : FdbAsyncIterator<TInput>
	{
		// ITERABLE

		private IFdbAsyncEnumerable<TInput> m_source;		// source sequence
		private int m_prefetchCount;						// max number of items to prefetch

		// ITERATOR

		private IFdbAsyncEnumerator<TInput> m_iterator;		// source.GetEnumerator()
		private Queue<TInput> m_buffer;						// buffer storing the items in the current window
		private Task<bool> m_nextTask;						// holds on to the last pending call to m_iterator.MoveNext() when our buffer is full
		private bool m_innerHasCompleted;					// set to true once m_iterator.MoveNext() has returned false

		/// <summary>Create a new batching iterator</summary>
		/// <param name="source">Source sequence of items that must be batched by waves</param>
		/// <param name="prefetchCount">Maximum size of a batch to return down the line</param>
		public FdbPrefetchingIterator(IFdbAsyncEnumerable<TInput> source, int prefetchCount)
		{
			Contract.Requires(source != null && prefetchCount > 0);

			m_source = source;
			m_prefetchCount = prefetchCount;
		}

		protected override FdbAsyncIterator<TInput> Clone()
		{
			return new FdbPrefetchingIterator<TInput>(m_source, m_prefetchCount);
		}

		protected override Task<bool> OnFirstAsync(CancellationToken ct)
		{
			// open the inner iterator

			IFdbAsyncEnumerator<TInput> iterator = null;
			Queue<TInput> buffer = null;
			try
			{
				iterator = m_source.GetEnumerator(m_mode);
				if (iterator == null)
				{
					m_innerHasCompleted = true;
					return TaskHelpers.FalseTask;
				}

				// pre-allocate the prefetching buffer
				buffer = new Queue<TInput>(m_prefetchCount);
				return TaskHelpers.TrueTask;
			}
			catch (Exception)
			{
				m_innerHasCompleted = true;
				buffer = null;
				if (iterator != null)
				{
					var tmp = iterator;
					iterator = null;
					tmp.Dispose();
				}
				throw;
			}
			finally
			{
				m_iterator = iterator;
				m_buffer = buffer;
			}
		}

		protected override Task<bool> OnNextAsync(CancellationToken ct)
		{
			if (m_buffer.Count > 0)
			{
				var nextTask = m_nextTask;
				if (nextTask == null || !m_nextTask.IsCompleted)
				{
					var current = m_buffer.Dequeue();
					return Publish(current) ? TaskHelpers.TrueTask : TaskHelpers.FalseTask;
				}
			}

			return PrefetchNextItemsAsync(ct);
		}

		protected virtual async Task<bool> PrefetchNextItemsAsync(CancellationToken ct)
		{
			// read items from the source until the next call to Inner.MoveNext() is not already complete, or we have filled our prefetch buffer, then returns the first item in the buffer.

			var t = m_nextTask;
			if (t != null)
			{ // we already have preloaded the next item...
				m_nextTask = null;
			}
			else
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
				m_innerHasCompleted = true;
				if (m_buffer.Count == 0)
				{ // that was the last batch!
					return Completed();
				}
			}

			var current = m_buffer.Dequeue();
			return Publish(current);
		}

		protected override void Cleanup()
		{
			try
			{
				var nextTask = m_nextTask;
				if (nextTask != null)
				{ // defuse the task, which should fail once we dispose the inner iterator below...
					if (!nextTask.IsCompleted)
					{
						nextTask.ContinueWith((t) => { var x = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
					}
					else
					{
						var x = nextTask.Exception;
					}
				}

				var iterator = m_iterator;
				if (iterator != null)
				{
					iterator.Dispose();
				}

			}
			finally
			{
				m_innerHasCompleted = true;
				m_iterator = null;
				m_buffer = null;
				m_nextTask = null;
			}
		}

	}
}
