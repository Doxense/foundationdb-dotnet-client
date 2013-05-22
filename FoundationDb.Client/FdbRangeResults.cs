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

#undef DEBUG_RANGE_PAGING

using FoundationDb.Client.Native;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client
{

	/// <summary>Query describing a GetRange operation that will be performed at a later time</summary>
	/// <remarks>This object is reusable</remarks>
	public sealed class FdbRangeQuery
	{
		/// <summary>Key selector describing the beginning of the range</summary>
		public FdbKeySelector Begin { get; internal set; }

		/// <summary>Key selector describing the end of the range</summary>
		public FdbKeySelector End { get; internal set; }

		/// <summary>Limit in number of rows to return</summary>
		public int Limit { get; internal set; }

		/// <summary>Limit in number of bytes to return</summary>
		public int TargetBytes { get; internal set; }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode Mode { get; internal set; }

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		public bool Snapshot { get; internal set; }

		/// <summary>Should the results returned in reverse order (from last key to first key)</summary>
		public bool Reverse { get; internal set; }

		//TODO: fluent API ?
	}

	/// <summary>Query describing an ongoing GetRange operation</summary>
	public sealed class FdbRangeResults : IDisposable
	{

		internal FdbRangeResults(FdbTransaction transaction, FdbRangeQuery query)
		{
			this.Transaction = transaction;
			this.Query = query;
			this.Remaining = query.Limit > 0 ? query.Limit : default(int?);

			this.Begin = query.Begin;
			this.End = query.End;
		}

		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		internal FdbTransaction Transaction { get; private set; }

		/// <summary>Query describing the GetRange operation parameters</summary>
		public FdbRangeQuery Query { get; private set; }

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

		/// <summary>Reads all the results in a single operation</summary>
		/// <param name="ct"></param>
		/// <returns>List of all the results</returns>
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
		public async Task<List<T>> ReadAllAsync<T>(Func<Slice, Slice, T> lambda, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			//TODO: process a batch while fetching the next ?

			var results = new List<T>();
			while (await this.MoveNextAsync(ct))
			{
				foreach (var kvp in this.Chunk)
				{
					results.Add(lambda(kvp.Key, kvp.Value));
				}
			}
			return results;
		}

		/// <summary>Task that will complete when the next page of results arrives. Will return true if a new page (with at least one result) is available, or false if no more results are available.</summary>
		/// <param name="ct"></param>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		public Task<bool> MoveNextAsync(CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			//TODO: check that Transaction is still valid
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
			
			this.Iteration++;

#if DEBUG_RANGE_PAGING
			Debug.WriteLine("FdbRangeResults.GetNextPageAsync(iter=" + this.Iteration + ") started");
#endif

			var future = FdbNative.TransactionGetRange(this.Transaction.Handle, this.Begin, this.End, this.Remaining.GetValueOrDefault(), this.Query.TargetBytes, this.Query.Mode, this.Iteration, this.Query.Snapshot, this.Query.Reverse);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					bool hasMore;

					//TODO: locking ?

					this.Chunk = GetKeyValueArrayResult(h, out hasMore);
					this.RowCount += this.Chunk.Length;
					this.HasMore = hasMore;
					// subtract number of row from the remaining allowed
					if (this.Remaining.HasValue) this.Remaining = this.Remaining.Value - this.Chunk.Length;

					this.AtEnd = !hasMore || (this.Remaining.HasValue && this.Remaining.Value <= 0);

					if (!this.AtEnd)
					{ // update begin..end so that next call will continue from where we left...
						var lastKey = this.Chunk[this.Chunk.Length - 1].Key;
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
					return this.Chunk.Length > 0;
				},
				ct
			);
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
			this.Query = null;
			this.Chunk = null;
			this.AtEnd = true;
			this.HasMore = false;
			this.Iteration = -1;
		}

		#endregion

	}


}
