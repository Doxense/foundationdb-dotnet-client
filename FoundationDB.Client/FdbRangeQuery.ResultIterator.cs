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

//enable this to enable verbose traces when doing paging
#undef DEBUG_RANGE_ITERATOR

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	public partial class FdbRangeQuery
	{

		/// <summary>Async iterator that fetches the results by batch, but return them one by one</summary>
		/// <typeparam name="TResult">Type of the results returned</typeparam>
		[DebuggerDisplay("State={m_state}, RemainingInBatch={m_remainingInBatch}, ReadLastBatch={m_lastBatchRead}")]
		private sealed class ResultIterator<TResult> : FdbAsyncEnumerable.AsyncIterator<TResult>
		{

			private FdbRangeQuery m_query;

			private FdbTransaction m_transaction;

			/// <summary>Lambda used to transform pairs of key/value into the expected result</summary>
			private Func<KeyValuePair<Slice, Slice>, TResult> m_resultTransform;


			/// <summary>Iterator used to read chunks from the database</summary>
			private IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>[]> m_chunkIterator;

			/// <summary>True if we have reached the last page</summary>
			private bool m_outOfChunks;

			/// <summary>Current chunk (may contain all records or only a segment at a time)</summary>
			private KeyValuePair<Slice, Slice>[] m_chunk;

			/// <summary>Number of remaining items in the current batch</summary>
			private int m_itemsRemainingInChunk;

			/// <summary>Offset in the current batch of the current item</summary>
			private int m_currentOffsetInChunk;

			#region IFdbAsyncEnumerator<T>...

			public ResultIterator(FdbRangeQuery query, FdbTransaction transaction, Func<KeyValuePair<Slice, Slice>, TResult> transform)
			{
				Contract.Requires(query != null && transform != null);

				m_query = query;
				m_transaction = transaction ?? query.Transaction;
				m_resultTransform = transform;
			}

			protected override FdbAsyncEnumerable.AsyncIterator<TResult> Clone()
			{
				return new ResultIterator<TResult>(m_query, m_transaction, m_resultTransform);
			}

			protected override Task<bool> OnFirstAsync(CancellationToken ct)
			{
				// on first call, setup the page iterator
				if (m_chunkIterator == null)
				{
					m_chunkIterator = new PagingIterator(m_query, m_transaction).GetEnumerator();
				}
				return TaskHelpers.TrueTask;
			}

			protected override Task<bool> OnNextAsync(CancellationToken ct)
			{
				if (m_itemsRemainingInChunk > 0)
				{ // we need can get another one from the batch

					return TaskHelpers.FromResult(ProcessNextItem());
				}

				if (m_outOfChunks)
				{ // we already read the last batch !
#if DEBUG_RANGE_ITERATOR
					Debug.WriteLine("No more items and it was the last batch");
#endif
					return FdbAsyncEnumerable.FalseTask;
				}

				// slower path, we need to actually read the first batch...
				m_chunk = null;
				m_currentOffsetInChunk = -1;
				return ReadAnotherBatchAsync(ct);
			}

			private async Task<bool> ReadAnotherBatchAsync(CancellationToken ct)
			{
				Contract.Requires(m_itemsRemainingInChunk == 0 && m_currentOffsetInChunk == -1 && !m_outOfChunks);

				// start reading the next batch
				if (await m_chunkIterator.MoveNext(ct).ConfigureAwait(false))
				{ // we got a new chunk !

					var chunk = m_chunkIterator.Current;

					//note: if the range is empty, we may have an empty chunk, that is equivalent to no chunk
					if (chunk != null && chunk.Length > 0)
					{
#if DEBUG_RANGE_ITERATOR
						Debug.WriteLine("Got a new chunk from page iterator: " + chunk.Length);
#endif
						m_chunk = chunk;
						m_itemsRemainingInChunk = chunk.Length;

						// transform the first one
						return ProcessNextItem();
					}
#if DEBUG_RANGE_ITERATOR
					Debug.WriteLine("Got empty chunk from page iterator!");
#endif
				}

#if DEBUG_RANGE_ITERATOR
				Debug.WriteLine("No more chunks from page iterator");
#endif
				m_outOfChunks = true;
				return Completed();
			}

			private bool ProcessNextItem()
			{
				++m_currentOffsetInChunk;
				--m_itemsRemainingInChunk;
				var result = m_resultTransform(m_chunk[m_currentOffsetInChunk]);
				return Publish(result);
			}

			#endregion

			#region LINQ

			public override FdbAsyncEnumerable.AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
			{
				return FdbAsyncEnumerable.Map(this, selector);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
			{
				return FdbAsyncEnumerable.Map(this, asyncSelector);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TResult> Where(Func<TResult, bool> predicate)
			{
				return FdbAsyncEnumerable.Filter(this, predicate);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
			{
				return FdbAsyncEnumerable.Filter(this, asyncPredicate);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
			{
				return FdbAsyncEnumerable.Flatten(this, selector);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
			{
				return FdbAsyncEnumerable.Flatten(this, asyncSelector);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TResult, TCollection, TNew> resultSelector)
			{
				return FdbAsyncEnumerable.Flatten(this, asyncCollectionSelector, resultSelector);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
			{
				return FdbAsyncEnumerable.Flatten(this, collectionSelector, resultSelector);
			}

			public override FdbAsyncEnumerable.AsyncIterator<TResult> Take(int limit)
			{
				return new ResultIterator<TResult>(m_query.Take(limit), m_transaction, m_resultTransform);
			}

			#endregion

			protected override void Cleanup()
			{
				try
				{
					if (m_chunkIterator != null)
					{
						m_chunkIterator.Dispose();
					}
				}
				finally
				{
					m_chunkIterator = null;
					m_chunk = null;
					m_outOfChunks = true;
					m_currentOffsetInChunk = 0;
					m_itemsRemainingInChunk = 0;
				}
			}

		}

	}

}
