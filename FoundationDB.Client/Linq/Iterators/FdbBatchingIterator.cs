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
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Iterator that packs a sequence of items into a sequence of fixed-size arrays.</summary>
	/// <typeparam name="TInput">Type the the items from the source sequence</typeparam>
	internal class FdbBatchingIterator<TInput> : FdbAsyncIterator<TInput[]>
	{
		// Typical use cas: to merge incoming streams of items into a sequence of arrays. This is basically the inverse of the SelectMany() operator.
		// This iterator should mostly be used on sequence that have either no latency (reading from an in-memory buffer) or where the latency is the same for each items.

		// ITERABLE

		private IFdbAsyncEnumerable<TInput> m_source;		// source sequence
		private int m_batchSize;							// size of the buffer

		// ITERATOR

		private IFdbAsyncEnumerator<TInput> m_iterator;		// source.GetEnumerator()
		private List<TInput> m_buffer;						// buffer storing the items in the current window
		private bool m_innerHasCompleted;					// set to true once m_iterator.MoveNext() has returned false

		/// <summary>Create a new batching iterator</summary>
		/// <param name="source">Source sequence of items that must be batched by waves</param>
		/// <param name="batchSize">Maximum size of a batch to return down the line</param>
		public FdbBatchingIterator(IFdbAsyncEnumerable<TInput> source, int batchSize)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (batchSize <= 0) throw new ArgumentOutOfRangeException("batchSize", batchSize, "Batch size must be at least one.");

			m_source = source;
			m_batchSize = batchSize;
		}

		protected override FdbAsyncIterator<TInput[]> Clone()
		{
			return new FdbBatchingIterator<TInput>(m_source, m_batchSize);
		}

		protected override Task<bool> OnFirstAsync(CancellationToken ct)
		{
			// open the inner iterator

			IFdbAsyncEnumerator<TInput> iterator = null;
			List<TInput> buffer = null;
			try
			{
				iterator = m_source.GetEnumerator(m_mode);
				if (iterator == null)
				{
					m_innerHasCompleted = true;
					return TaskHelpers.FalseTask;
				}

				// pre-allocate the inner buffer, if it is not too big
				buffer = new List<TInput>(Math.Min(m_batchSize, 1024));
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

		protected override async Task<bool> OnNextAsync(CancellationToken ct)
		{
			// read items from the source until the buffer is full, or the source has completed

			if (m_innerHasCompleted)
			{
				return Completed();
			}

			bool hasMore = await m_iterator.MoveNext(ct).ConfigureAwait(false);
			while(hasMore && !ct.IsCancellationRequested)
			{
				m_buffer.Add(m_iterator.Current);
				if (m_buffer.Count >= m_batchSize) break;

				hasMore = await m_iterator.MoveNext(ct).ConfigureAwait(false);
			}
			ct.ThrowIfCancellationRequested();

			if (!hasMore)
			{
				m_innerHasCompleted = true;
				if (m_buffer.Count == 0)
				{ // no more items
					return Completed();
				}
			}

			var items = m_buffer.ToArray();
			m_buffer.Clear();
			return Publish(items);
		}

		protected override void Cleanup()
		{
			try
			{
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
			}
		}

	}
}
