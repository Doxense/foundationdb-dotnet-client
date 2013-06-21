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

namespace FoundationDb.Client
{
	using FoundationDb.Client.Utils;
	using FoundationDb.Linq;
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
		private sealed class ResultIterator<TResult> : IFdbAsyncEnumerator<TResult>
		{
			private const int STATE_INIT = 0;
			private const int STATE_READY = 1;
			private const int STATE_COMPLETED = 2;
			private const int STATE_DISPOSED = -1;

			/// <summary>State of the iterator</summary>
			private volatile int m_state = STATE_INIT;

			private FdbRangeQuery m_query;

			private FdbTransaction m_transaction;

			/// <summary>Lambda used to transform pairs of key/value into the expected result</summary>
			private Func<KeyValuePair<Slice, Slice>, TResult> m_resultTransform;

			/// <summary>Holds the current result</summary>
			private TResult m_current;

			/// <summary>Iterator used to read chunks from the database</summary>
			private PagingIterator m_chunkIterator;

			/// <summary>True if we have reached the last page</summary>
			private bool m_outOfChunks;

			/// <summary>Current chunk (may contain all records or only a segment at a time)</summary>
			private KeyValuePair<Slice, Slice>[] m_chunk;

			/// <summary>Number of remaining items in the current batch</summary>
			private int m_itemsRemainingInChunk;

			/// <summary>Offset in the current batch of the current item</summary>
			private int m_currentOffsetInChunk;

			#region IFdbAsyncEnumerator<T>...

			public ResultIterator(FdbRangeQuery query, Func<KeyValuePair<Slice, Slice>, TResult> transform)
			{
				Contract.Requires(query != null && transform != null);

				m_query = query;
				m_transaction = query.Transaction;
				m_resultTransform = transform;
			}

			public Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested();

				switch (m_state)
				{
					case STATE_COMPLETED:
					{ // already reached the end !
						return FdbAsyncEnumerable.FalseTask;
					}
					case STATE_INIT:
					{
#if DEBUG_RANGE_ITERATOR
						Debug.WriteLine("Initializing range query iterator for first chunk");
#endif
						// on first call, setup the page iterator
						if (m_chunkIterator == null)
						{
							m_chunkIterator = new PagingIterator(m_query);
						}
						break;
					}
					case STATE_READY:
					{
						break;
					}
					default:
					{
						ThrowInvalidState();
						break;
					}
				}

				if (m_itemsRemainingInChunk > 0)
				{ // we need can get another one from the batch

					return ProcessNextItem();
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
				return ReadAnotherBatchAsync(cancellationToken);
			}

			private async Task<bool> ReadAnotherBatchAsync(CancellationToken ct)
			{
				Contract.Requires(m_state == STATE_INIT || m_state == STATE_READY);
				Contract.Requires(m_itemsRemainingInChunk == 0 && m_currentOffsetInChunk == -1 && !m_outOfChunks);

				// start reading the next batch
				if (await m_chunkIterator.MoveNext(ct))
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
						await ProcessNextItem();

						m_state = STATE_READY;
						return true;
					}
#if DEBUG_RANGE_ITERATOR
					Debug.WriteLine("Got empty chunk from page iterator!");
#endif
				}

#if DEBUG_RANGE_ITERATOR
				Debug.WriteLine("No more chunks from page iterator");
#endif
				m_outOfChunks = true;
				m_current = default(TResult);
				m_state = STATE_COMPLETED;
				return false;
			}

			public TResult Current
			{
				get
				{
					if (m_state != STATE_READY) ThrowInvalidState();
					return m_current;
				}
			}

			private Task<bool> ProcessNextItem()
			{
				++m_currentOffsetInChunk;
				--m_itemsRemainingInChunk;
				m_current = m_resultTransform(m_chunk[m_currentOffsetInChunk]);
#if DEBUG_RANGE_ITERATOR
				Debug.WriteLine("Using item #" + m_currentOffsetInChunk + " in current chunk (" + m_itemsRemainingInChunk + " remaining)");
#endif
				return FdbAsyncEnumerable.TrueTask;
			}

			#endregion

			private void ThrowInvalidState()
			{
				switch (m_state)
				{
					case STATE_DISPOSED: throw new ObjectDisposedException(null, "Query iterator has already been disposed");
					case STATE_READY: return;
					case STATE_INIT: throw new InvalidOperationException("You must call MoveNext at least once before accessing the current value");
					case STATE_COMPLETED: throw new ObjectDisposedException(null, "Query iterator has already completed");
					default: throw new InvalidOperationException("Invalid unknown state");
				}
			}

			private void ThrowIfDisposed()
			{
				if (m_state == STATE_DISPOSED) throw new ObjectDisposedException(null, "Query iterator has already been disposed");
			}

			public void Dispose()
			{
				//TODO: should we wait/cancel any pending read task ?

				if (m_state != STATE_DISPOSED)
				{
					m_state = STATE_DISPOSED;
#if DEBUG_RANGE_ITERATOR
					Debug.WriteLine("Disposing range query iterator");
#endif

					try
					{
						if (m_chunkIterator != null)
						{
							m_chunkIterator.Dispose();
						}
					}
					finally
					{
						m_current = default(TResult);
						m_query = null;
						m_transaction = null;
						m_chunkIterator = null;
						m_chunk = null;
						m_outOfChunks = true;
						m_currentOffsetInChunk = 0;
						m_itemsRemainingInChunk = 0;
					}
				}
				GC.SuppressFinalize(this);
			}
		}

	}

}
