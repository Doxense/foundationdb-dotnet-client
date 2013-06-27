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
#undef DEBUG_RANGE_PAGING

namespace FoundationDB.Client
{
	using FoundationDB.Client.Native;
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
		private sealed class PagingIterator : IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>[]>
		{
			private const int STATE_INIT = 0;
			private const int STATE_READY = 1;
			private const int STATE_COMPLETED = 2;
			private const int STATE_DISPOSED = -1;


			/// <summary>State of the iterator</summary>
			private volatile int m_state = STATE_INIT;

			// --

			private FdbRangeQuery Query { get; set; }

			private FdbTransaction Transaction { get; set; }

			/// <summary>Key selector describing the beginning of the current range (when paging)</summary>
			private FdbKeySelector Begin { get; set; }

			/// <summary>Key selector describing the end of the current range (when paging)</summary>
			private FdbKeySelector End { get; set; }

			/// <summary>If non null, contains the remaining allowed number of rows</summary>
			private int? Remaining { get; set; }

			/// <summary>Iteration number of current page (in iterator mode)</summary>
			private int Iteration { get; set; }

			/// <summary>Current page (may contain all records or only a segment at a time)</summary>
			private KeyValuePair<Slice, Slice>[] Chunk { get; set; }

			/// <summary>If true, we have more records pending</summary>
			private bool HasMore { get; set; }

			/// <summary>True if we have reached the last page</summary>
			private bool AtEnd { get; set; }

			/// <summary>Running total of rows that have been read</summary>
			private int RowCount { get; set; }

			/// <summary>Current/Last batch read task</summary>
			private Task<bool> PendingReadTask { get; set; }

			// --

			#region IFdbAsyncEnumerator<T>...

			public PagingIterator(FdbRangeQuery query)
			{
				Contract.Requires(query != null);

				this.Query = query;
				this.Transaction = query.Transaction;

				this.Remaining = query.Limit > 0 ? query.Limit : default(int?);

				this.Begin = query.Range.Start;
				this.End = query.Range.Stop;
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

				//TODO: check if not already pending
				if (PendingReadTask != null && !PendingReadTask.IsCompleted)
				{
					throw new InvalidOperationException("Cannot call MoveNext while a previous call is still running");
				}

				if (this.AtEnd)
				{ // we already read the last batch !
					m_state = STATE_COMPLETED;
					return FdbAsyncEnumerable.FalseTask;
				}

				// slower path, we need to actually read the first batch...
				return FetchNextPageAsync(cancellationToken);
			}

			public KeyValuePair<Slice, Slice>[] Current
			{
				get
				{
					if (m_state != STATE_READY) ThrowInvalidState();
					return this.Chunk;
				}
			}

			#endregion

			#region ...

			/// <summary>Asynchronously fetch a new page of results</summary>
			/// <param name="ct"></param>
			/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
			private Task<bool> FetchNextPageAsync(CancellationToken ct)
			{
				Contract.Requires(!this.AtEnd);
				Contract.Requires(this.Iteration >= 0);

				// Make sure that we are not called while the previous fetch is still running
				if (this.PendingReadTask != null && !this.PendingReadTask.IsCompleted)
				{
					throw new InvalidOperationException("Cannot fetch another page while a previous read operation is still pending");
				}

				ct.ThrowIfCancellationRequested();
				this.Transaction.EnsuresCanReadOrWrite(ct);

				this.Iteration++;

#if DEBUG_RANGE_PAGING
				Debug.WriteLine("FdbRangeQuery.PagingIterator.FetchNextPageAsync(iter=" + this.Iteration + ") started");
#endif

				var future = FdbNative.TransactionGetRange(this.Transaction.Handle, this.Begin, this.End, this.Remaining.GetValueOrDefault(), this.Query.TargetBytes, this.Query.Mode, this.Iteration, this.Query.Snapshot, this.Query.Reverse);
				var task = FdbFuture.CreateTaskFromHandle(
					future,
					(h) =>
					{
						if (this.Transaction == null)
						{ // disposed ? quietly return
							return false;
						}

						//TODO: locking ?

						bool hasMore;
						var chunk = GetKeyValueArrayResult(h, out hasMore);
						this.Chunk = chunk;
						this.RowCount += chunk.Length;
						this.HasMore = hasMore;
						// subtract number of row from the remaining allowed
						if (this.Remaining.HasValue) this.Remaining = this.Remaining.Value - chunk.Length;

						this.AtEnd = !hasMore || (this.Remaining.HasValue && this.Remaining.Value <= 0);

						m_state = STATE_READY;

						if (!this.AtEnd)
						{ // update begin..end so that next call will continue from where we left...
							var lastKey = chunk[chunk.Length - 1].Key;
							if (this.Query.Reverse)
							{
								this.End = FdbKeySelector.FirstGreaterOrEqual(lastKey);
							}
							else
							{
								this.Begin = FdbKeySelector.FirstGreaterThan(lastKey);
							}
						}
#if DEBUG_RANGE_PAGING
						Debug.WriteLine("FdbRangeQuery.PagingIterator.FetchNextPageAsync() returned " + this.Chunk.Length + " results (" + this.RowCount + " total) " + (hasMore ? " with more to come" : " and has no more data"));
#endif
						return chunk.Length > 0 && this.Transaction != null;
					},
					ct
				);

				// keep track of this operation
				this.PendingReadTask = task;
				return task;
			}

			/// <summary>Extract a chunk of result from a completed Future</summary>
			/// <param name="h">Handle to the completed Future</param>
			/// <param name="more">Receives true if there are more result, or false if all results have been transmited</param>
			/// <returns></returns>
			private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResult(FutureHandle h, out bool more)
			{
				KeyValuePair<Slice, Slice>[] result;
				var err = FdbNative.FutureGetKeyValueArray(h, out result, out more);
#if DEBUG_RANGE_PAGING
				Debug.WriteLine("FdbRangeQuery.PagingIterator.GetKeyValueArrayResult() => err=" + err + ", result=" + (result != null ? result.Length : -1) + ", more=" + more);
#endif
				Fdb.DieOnError(err);
				return result;
			}

			#endregion

			private void ThrowInvalidState()
			{
				switch(m_state)
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

				m_state = STATE_DISPOSED;

				this.Chunk = null;
				this.AtEnd = true;
				this.HasMore = false;
				this.Remaining = null;
				this.Iteration = -1;
				this.Query = null;
				this.Transaction = null;
				this.PendingReadTask = null;
			}
		}

	}

}
