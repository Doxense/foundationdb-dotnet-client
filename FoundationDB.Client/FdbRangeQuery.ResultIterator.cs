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

//enable this to enable verbose traces when doing paging
#undef DEBUG_RANGE_ITERATOR

namespace FoundationDB.Client
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using FoundationDB.Linq;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	public partial class FdbRangeQuery<T>
	{

		/// <summary>Async iterator that fetches the results by batch, but return them one by one</summary>
		[DebuggerDisplay("State={m_state}, Current={m_current}, RemainingInChunk={m_itemsRemainingInChunk}, OutOfChunks={m_outOfChunks}")]
		private sealed class ResultIterator : FdbAsyncIterator<T>
		{

			private readonly FdbRangeQuery<T> m_query;

			private readonly IFdbReadOnlyTransaction m_transaction;

			/// <summary>Lambda used to transform pairs of key/value into the expected result</summary>
			private readonly Func<KeyValuePair<Slice, Slice>, T> m_resultTransform;

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

			public ResultIterator([NotNull] FdbRangeQuery<T> query, IFdbReadOnlyTransaction transaction, [NotNull] Func<KeyValuePair<Slice, Slice>, T> transform)
			{
				Contract.Requires(query != null && transform != null);

				m_query = query;
				m_transaction = transaction ?? query.Transaction;
				m_resultTransform = transform;
			}

			protected override FdbAsyncIterator<T> Clone()
			{
				return new ResultIterator(m_query, m_transaction, m_resultTransform);
			}

			protected override Task<bool> OnFirstAsync(CancellationToken cancellationToken)
			{
				// on first call, setup the page iterator
				if (m_chunkIterator == null)
				{
					m_chunkIterator = new PagingIterator(m_query, m_transaction).GetEnumerator(m_mode);
				}
				return TaskHelpers.TrueTask;
			}

			protected override Task<bool> OnNextAsync(CancellationToken cancellationToken)
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
					return TaskHelpers.FalseTask;
				}

				// slower path, we need to actually read the first batch...
				m_chunk = null;
				m_currentOffsetInChunk = -1;
				return ReadAnotherBatchAsync(cancellationToken);
			}

			private async Task<bool> ReadAnotherBatchAsync(CancellationToken cancellationToken)
			{
				Contract.Requires(m_itemsRemainingInChunk == 0 && m_currentOffsetInChunk == -1 && !m_outOfChunks);

				var iterator = m_chunkIterator;

				// start reading the next batch
				if (await iterator.MoveNext(cancellationToken).ConfigureAwait(false))
				{ // we got a new chunk !

					//note: Dispose() or Cleanup() maybe have been called concurrently!
					ThrowInvalidState();

					var chunk = iterator.Current;

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

			public override FdbAsyncIterator<R> Select<R>(Func<T, R> selector)
			{
				var query = new FdbRangeQuery<R>(
					m_transaction,
					m_query.Begin,
					m_query.End,
					(x) => selector(m_resultTransform(x)),
					m_query.Snapshot,
					m_query.Options
				);

				return new FdbRangeQuery<R>.ResultIterator(query, m_transaction, query.Transform);
			}

			public override FdbAsyncIterator<T> Take(int limit)
			{
				return new ResultIterator(m_query.Take(limit), m_transaction, m_resultTransform);
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
