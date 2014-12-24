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

namespace FoundationDB.Linq
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Packs items from an inner sequence, into a sequence of fixed-size arrays.</summary>
	/// <typeparam name="TInput">Type the the items from the source sequence</typeparam>
	internal class FdbBatchingAsyncIterator<TInput> : FdbAsyncFilterIterator<TInput, TInput[]>
	{
		// Typical use cas: to merge incoming streams of items into a sequence of arrays. This is basically the inverse of the SelectMany() operator.
		// This iterator should mostly be used on sequence that have either no latency (reading from an in-memory buffer) or where the latency is the same for each items.

		// ITERABLE

		private int m_batchSize;							// size of the buffer

		// ITERATOR

		private List<TInput> m_buffer;						// buffer storing the items in the current window

		/// <summary>Create a new batching iterator</summary>
		/// <param name="source">Source sequence of items that must be batched by waves</param>
		/// <param name="batchSize">Maximum size of a batch to return down the line</param>
		public FdbBatchingAsyncIterator(IFdbAsyncEnumerable<TInput> source, int batchSize)
			: base(source)
		{
			Contract.Requires(batchSize > 0);

			m_source = source;
			m_batchSize = batchSize;
		}

		protected override FdbAsyncIterator<TInput[]> Clone()
		{
			return new FdbBatchingAsyncIterator<TInput>(m_source, m_batchSize);
		}

		protected override void OnStarted(IFdbAsyncEnumerator<TInput> iterator)
		{
			// pre-allocate the inner buffer, if it is not too big
			m_buffer = new List<TInput>(Math.Min(m_batchSize, 1024));
		}

		protected override async Task<bool> OnNextAsync(CancellationToken ct)
		{
			// read items from the source until the buffer is full, or the source has completed

			if (m_innerHasCompleted)
			{
				return Completed();
			}

			var iterator = m_iterator;
			var buffer = m_buffer;

			bool hasMore = await iterator.MoveNext(ct).ConfigureAwait(false);

			while(hasMore && !ct.IsCancellationRequested)
			{
				buffer.Add(iterator.Current);
				if (buffer.Count >= m_batchSize) break;

				hasMore = await iterator.MoveNext(ct).ConfigureAwait(false);
			}
			ct.ThrowIfCancellationRequested();

			if (!hasMore)
			{
				MarkInnerAsCompleted();
				if (buffer.Count == 0)
				{ // no more items
					return Completed();
				}
			}

			var items = buffer.ToArray();
			buffer.Clear();
			return Publish(items);
		}

		protected override void OnStopped()
		{
			m_buffer = null;
		}

	}
}
