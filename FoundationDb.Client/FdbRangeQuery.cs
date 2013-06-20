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
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Client
{
	using FoundationDb.Client.Native;
	using FoundationDb.Client.Utils;
	using FoundationDb.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Query describing an ongoing GetRange operation</summary>
	[DebuggerDisplay("Begin={Query.Begin}, End={Query.End}, Chunk={Chunk==null?-1:Chunk.Length}, HasMore={HasMore}, Iteration={Iteration}, Remaining={Remaining}")]
	public sealed class FdbRangeQuery : IDisposable, IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>>
	{

		/// <summary>Async iterator that fetches the results by batch, but return them one by one</summary>
		/// <typeparam name="TResult">Type of the results returned</typeparam>
		[DebuggerDisplay("State={m_state}, RemainingInBatch={m_remainingInBatch}, ReadLastBatch={m_lastBatchRead}")]
		private sealed class Iterator<TResult> : IFdbAsyncEnumerator<TResult>
		{
			private const int STATE_INIT = 0;
			private const int STATE_READY = 1;
			private const int STATE_COMPLETED = 2;
			private const int STATE_DISPOSED = -1;

			//TODO: move read logic from query to this class!

			private FdbRangeQuery m_query;

			private Func<KeyValuePair<Slice, Slice>, TResult> m_transform;

			/// <summary>State of the iterator</summary>
			private volatile int m_state = STATE_INIT;

			/// <summary>Holds the current result</summary>
			private TResult m_current;

			/// <summary>Current/Last batch read task</summary>
			private Task<bool> m_nextBatchReadTask;
			/// <summary>True if we already have read the last batch</summary>
			private bool m_lastBatchRead;

			/// <summary>Last successfully read batch of results</summary>
			private KeyValuePair<Slice, Slice>[] m_batch;
			/// <summary>Number of remaining items in the current batch</summary>
			private int m_remainingInBatch;
			/// <summary>Offset in the current batch of the current item</summary>
			private int m_offset;

			public Iterator(FdbRangeQuery query, Func<KeyValuePair<Slice, Slice>, TResult> transform)
			{
				Contract.Requires(query != null && transform != null);

				m_query = query;
				m_transform = transform;
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
				if (m_nextBatchReadTask != null && !m_nextBatchReadTask.IsCompleted)
				{
					throw new InvalidOperationException("Cannot call MoveNext while a previous call is still running");
				}

				if (m_remainingInBatch > 0)
				{ // we need can get another one from the batch
					++m_offset;
					SetCurrentItem();
					m_remainingInBatch--;
					return FdbAsyncEnumerable.TrueTask;
				}

				if (m_lastBatchRead)
				{ // we already read the last batch !
					return FdbAsyncEnumerable.FalseTask;
				}

				// slower path, we need to actually read the first batch...
				m_offset = 0;
				m_batch = null;
				return ReadAnotherBatchAsync(cancellationToken);
			}

			private async Task<bool> ReadAnotherBatchAsync(CancellationToken ct)
			{
				Contract.Requires(m_state == STATE_INIT || m_state == STATE_READY);
				Contract.Requires(m_remainingInBatch == 0 && m_offset == 0 && !m_lastBatchRead);

				// start reading the next batch
				m_nextBatchReadTask = m_query.MoveNextAsync(ct);

				if (await m_nextBatchReadTask)
				{ // we got a new chunk !
					m_batch = m_query.Chunk;
					m_remainingInBatch = m_batch.Length;
				}

				if (m_remainingInBatch > 0)
				{
					// read the first one
					m_offset = 0;
					SetCurrentItem();
					--m_remainingInBatch;
					m_state = STATE_READY;
					return true;
				}

				m_lastBatchRead = true;
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

			private void SetCurrentItem()
			{
				m_current = m_transform(m_batch[m_offset]);
			}

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

			public void Dispose()
			{
				//TODO: should we wait/cancel any pending read task ?

				m_state = -2;
				m_query = null;
				m_current = default(TResult);
				m_nextBatchReadTask = null;
				m_lastBatchRead = true;
				m_batch = null;
				m_offset = 0;
				m_remainingInBatch = 0;
			}
		}

		internal FdbRangeQuery(FdbTransaction transaction, FdbRangeSelector query)
		{
			this.Transaction = transaction;
			this.Query = query;
			this.Remaining = query.Limit > 0 ? query.Limit : default(int?);

			this.Begin = query.Begin;
			this.End = query.End;
		}

		//TODO: move read logic into BatchedITerator !

		/// <summary>Holds to the current or last read task</summary>
		private Task PendingTask { get; set; }

		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		internal FdbTransaction Transaction { get; private set; }

		/// <summary>Query describing the GetRange operation parameters</summary>
		public FdbRangeSelector Query { get; private set; }

		/// <summary>Key selector describing the beginning of the current range (when paging)</summary>
		internal FdbKeySelector Begin { get; private set; }

		/// <summary>Key selector describing the end of the current range (when paging)</summary>
		internal FdbKeySelector End { get; private set; }

		/// <summary>If non null, contains the remaining allowed number of rows</summary>
		internal int? Remaining { get; private set; }

		/// <summary>Iteration number of current page (in iterator mode)</summary>
		public int Iteration { get; private set; }

		/// <summary>Current page (may contain all records or only a segment at a time)</summary>
		internal KeyValuePair<Slice, Slice>[] Chunk { get; private set; }

		/// <summary>If true, we have more records pending</summary>
		public bool HasMore { get; private set; }

		/// <summary>True if we have reached the last page</summary>
		private bool AtEnd { get; set; }

		/// <summary>Running total of rows that have been read</summary>
		private int RowCount { get; set; }

#if REFACTORED

		/// <summary>Reads all the results in a single operation</summary>
		/// <returns>List of all the results</returns>
		[Obsolete("Use ToListAsync() instead")]
		public async Task<List<KeyValuePair<Slice, Slice>>> ReadAllAsync(CancellationToken ct = default(CancellationToken))
		{
			Contract.Requires(this.Iteration >= 0);

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			var results = new List<KeyValuePair<Slice, Slice>>();
			while (await this.MoveNextAsync(ct))
			{
				results.AddRange(this.Chunk);
			}
			return results;
		}

		/// <summary>Reads all the results in a single operation, and process the results as they arrive</summary>
		/// <typeparam name="T">Type of the processed results</typeparam>
		/// <param name="lambda">Lambda function called to process each result</param>
		/// <param name="ct"></param>
		/// <returns>List of all processed results</returns>
		[Obsolete("Use .Select().ToListAsync() instead")]
		public async Task<List<T>> ReadAllAsync<T>(Func<KeyValuePair<Slice, Slice>, T> lambda, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			var results = new List<T>();
			//TODO: pre-allocate the list to something more sensible ?

			while (await this.MoveNextAsync(ct))
			{
				//TODO: process a batch while fetching the next ?
				foreach (var kvp in this.Chunk)
				{
					results.Add(lambda(kvp));
				}
			}
			return results;
		}

		/// <summary>Reads all the results in a single operation, and process the results as they arrive</summary>
		/// <typeparam name="T">Type of the processed results</typeparam>
		/// <param name="lambda">Lambda function called to process each result</param>
		/// <param name="ct"></param>
		/// <returns>List of all processed results</returns>
		[Obsolete("Use .Select().ToListAsync() instead")]
		public async Task<List<T>> ReadAllAsync<T>(Func<Slice, Slice, T> lambda, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			var results = new List<T>();
			//TODO: pre-allocate the list to something more sensible ?

			while (await this.MoveNextAsync(ct))
			{
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				//TODO: process a batch while fetching the next ?
				foreach (var kvp in this.Chunk)
				{
					results.Add(lambda(kvp.Key, kvp.Value));
				}
			}
			return results;
		}

		/// <summary>Reads all the results in a single operation, and process the results as they arrive</summary>
		/// <typeparam name="T">Type of the processed results</typeparam>
		/// <param name="lambda">Lambda function called to process each result</param>
		/// <param name="ct"></param>
		/// <returns>List of all processed results</returns>
		[Obsolete("Use .Select().ToListAsync() instead")]
		public async Task<List<KeyValuePair<K, V>>> ReadAllAsync<K, V>(Func<Slice, K> keyReader, Func<Slice, V> valueReader, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			var results = new List<KeyValuePair<K, V>>();
			//TODO: pre-allocate the list to something more sensible ?

			while (await this.MoveNextAsync(ct))
			{
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				//TODO: process a batch while fetching the next ?
				foreach (var kvp in this.Chunk)
				{
					var key = keyReader(kvp.Key);
					var value = valueReader(kvp.Value);

					results.Add(new KeyValuePair<K, V>(key, value));
				}
			}
			return results;
		}

		/// <summary>Reads all the results in a single operation, and process the results as they arrive</summary>
		/// <typeparam name="T">Type of the processed results</typeparam>
		/// <param name="lambda">Lambda function called to process each result</param>
		/// <param name="ct"></param>
		/// <returns>List of all processed results</returns>
		[Obsolete("Use .Select().ToListAsync() instead")]
		public async Task<List<T>> ReadAllAsync<K, V, T>(Func<Slice, K> keyReader, Func<Slice, V> valueReader, Func<K, V, T> transform, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			var results = new List<T>();
			//TODO: pre-allocate the list to something more sensible ?

			while (await this.MoveNextAsync(ct))
			{
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				//TODO: process a batch while fetching the next ?
				foreach (var kvp in this.Chunk)
				{
					var key = keyReader(kvp.Key);
					var value = valueReader(kvp.Value);

					results.Add(transform(key, value));
				}
			}
			return results;
		}

		/// <summary>Reads all the results in a single operation, and process the results as they arrive, do not store anything</summary>
		/// <param name="action">delegate function called to process each result</param>
		/// <param name="ct"></param>
		[Obsolete("Use .ForEachAsync() instead")]
		public async Task ExecuteAllAsync(Action<Slice, Slice> action, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			while (await this.MoveNextAsync(ct))
			{
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				//TODO: process a batch while fetching the next ?
				foreach (var kvp in this.Chunk)
				{
					action(kvp.Key, kvp.Value);
				}
			}
		}

		/// <summary>Reads all the results in a single operation, and process the results as they arrive, do not store anything</summary>
		/// <param name="action">delegate function called to process each result</param>
		/// <param name="ct"></param>
		[Obsolete("Use .ForEachAsync() instead")]
		public async Task ExecuteAllAsync(Action<KeyValuePair<Slice, Slice>> action, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			while (await this.MoveNextAsync(ct))
			{
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				//TODO: process a batch while fetching the next ?
				foreach (var kvp in this.Chunk)
				{
					action(kvp);
				}
			}
		}

#endif

		/// <summary>[EXPERIMENTAL] Execute a handle on chunk as they arrive, while fetching the next one in the background</summary>
		/// <param name="chunkHandler">Handler that gets called every time a new chunk arrives. Receives the chunk array in the first paramter, a 'first' boolean in the second parameter, and a cancellation token in the third</param>
		/// <param name="ct">Cancellation Token</param>
		/// <returns></returns>
		public async Task ExecuteAsync(Action<KeyValuePair<Slice, Slice>[], bool, CancellationToken> chunkHandler, CancellationToken ct)
		{
			Contract.Requires(chunkHandler != null);

			ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			// We should not have already ran !
			if (this.PendingTask != null) throw new InvalidOperationException("Can only execute once per query");

			var error = default(ExceptionDispatchInfo);
			//note: can replaced by manual Exception holding/rethrowing on .NET 4.0

			// schedule first read
			Task<bool> readTask = MoveNextAsync(ct);
			bool first = true;

			while(await readTask)
			{
				readTask = null;

				// capture current chunk
				var chunk = this.Chunk;
				
				// schedule next read immediately
				if (!ct.IsCancellationRequested)
				{
					readTask = MoveNextAsync(ct);
				}

				// run handler on current chunk
				try
				{
					chunkHandler(chunk, first, ct);
					first = false;
				}
				catch (Exception e)
				{
					if (e is StackOverflowException || e is OutOfMemoryException || e is ThreadAbortException) throw;

					// instead of cancelling the next read, we will let it continue (should be quick)
					error = ExceptionDispatchInfo.Capture(e);
					break;
				}
			}

			if (readTask != null && !readTask.IsCompleted)
			{
				//TODO: should we cancel it ?
				// just await it for now
				try
				{
					await readTask;
				}
				catch (Exception x)
				{
					// We are more interseted in the previous exception..
					if (error != null) error = ExceptionDispatchInfo.Capture(x);
					throw;
				}
			}

			if (error != null)
			{
				error.Throw();
			}
		}

		/// <summary>Task that will complete when the next page of results arrives. Will return true if a new page (with at least one result) is available, or false if no more results are available.</summary>
		/// <param name="ct"></param>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		public Task<bool> MoveNextAsync(CancellationToken ct = default(CancellationToken))
		{
			// Ensures we are still alive
			ThrowIfDisposed();
			this.Transaction.EnsuresCanReadOrWrite(ct);

			//TODO: ensure that nobody calls us while a fetch is still running

			// first time ?
			if (!this.AtEnd)
			{
				return FetchNextPageAsync(ct);
			}
			else
			{
				return Task.FromResult(false);
			}
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <param name="ct"></param>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		private Task<bool> FetchNextPageAsync(CancellationToken ct)
		{
			Contract.Requires(!this.AtEnd);
			Contract.Requires(this.Iteration >= 0);

			// Make sure that we are not called while the previous fetch is still running
			if (this.PendingTask != null && !this.PendingTask.IsCompleted)
			{
				throw new InvalidOperationException("Cannot fetch another page while a previous read operation is still pending");
			}

			ct.ThrowIfCancellationRequested();

			this.Iteration++;

#if DEBUG_RANGE_PAGING
			Debug.WriteLine("FdbRangeResults.GetNextPageAsync(iter=" + this.Iteration + ") started");
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
					Debug.WriteLine("FdbRangeResults.GetFirstPage() returned " + this.Chunk.Length + " results (" + this.RowCount + " total) " + (hasMore ? " with more to come" : " and has no more data"));
#endif
					return chunk.Length > 0 && this.Transaction != null;
				},
				ct
			);

			// keep track of this operation
			this.PendingTask = task;
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
			Debug.WriteLine("FdbRangeResults.GetKeyValueArrayResult() => err=" + err + ", result=" + (result != null ? result.Length : -1) + ", more=" + more);
#endif
			Fdb.DieOnError(err);
			return result;
		}

		#region IDisposable Members...

		private void ThrowIfDisposed()
		{
			if (this.Transaction == null) throw new ObjectDisposedException(this.GetType().Name);
		}

		public void Dispose()
		{
			//TODO: locking ?
			this.Transaction = null;
			this.PendingTask = null;
			this.Query = null;
			this.Chunk = null;
			this.AtEnd = true;
			this.HasMore = false;
			this.Remaining = null;
			this.Iteration = -1;
		}

		#endregion

		#region Pseudo-LINQ

		private bool m_gotUsedOnce;

		public IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>> GetEnumerator()
		{
			ThrowIfDisposed();
			if (m_gotUsedOnce) throw new InvalidOperationException("This query has already been executed once. Reusing the same query object would re-run the query on the server. If you need to data multiple times, you should call ToListAsync() one time, and then reuse this list using normal LINQ to Object operators.");
			m_gotUsedOnce = true;
			return new Iterator<KeyValuePair<Slice, Slice>>(this, x => x);
		}

		public Task<List<KeyValuePair<Slice, Slice>>> ToListAsync(CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.ToListAsync(this, ct);
		}

		public IFdbAsyncEnumerable<T> Select<T>(Func<KeyValuePair<Slice, Slice>, T> lambda)
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, lambda);
		}

		public IFdbAsyncEnumerable<T> Select<T>(Func<Slice, Slice, T> lambda)
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, (kvp) => lambda(kvp.Key, kvp.Value));
		}

		public IFdbAsyncEnumerable<KeyValuePair<K, V>> Select<K, V>(Func<Slice, K> extractKey, Func<Slice, V> extractValue)
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, (kvp) => new KeyValuePair<K, V>(extractKey(kvp.Key), extractValue(kvp.Value)));
		}

		public IFdbAsyncEnumerable<R> Select<K, V, R>(Func<Slice, K> extractKey, Func<Slice, V> extractValue, Func<K, V, R> transform)
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, (kvp) => transform(extractKey(kvp.Key), extractValue(kvp.Value)));
		}

		public IFdbAsyncEnumerable<Slice> Keys()
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, kvp => kvp.Key);
		}

		public IFdbAsyncEnumerable<T> Keys<T>(Func<Slice, T> lambda)
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, kvp => lambda(kvp.Key));
		}

		public IFdbAsyncEnumerable<Slice> Values()
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, kvp => kvp.Value);
		}

		public IFdbAsyncEnumerable<T> Values<T>(Func<Slice, T> lambda)
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.Select(this, kvp => lambda(kvp.Value));
		}

		public Task ForEachAsync(Action<KeyValuePair<Slice, Slice>> action, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.ForEachAsync(this, action, ct);
		}

		public Task ForEachAsync(Action<Slice, Slice> action, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			return FdbAsyncEnumerable.ForEachAsync(this, (kvp) => action(kvp.Key, kvp.Value), ct);
		}

		#endregion

	}

}
