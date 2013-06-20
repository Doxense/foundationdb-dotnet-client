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

namespace FoundationDb.Client
{
	using FoundationDb.Client.Utils;
	using FoundationDb.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Query describing an ongoing GetRange operation</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}, Limit={Limit}, Mode={Mode}, Reverse={Reverse}, Snapshot={Snapshot}")]
	public partial class FdbRangeQuery : IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>>
	{

		/// <summary>Construct a query with a set of initial settings</summary>
		internal FdbRangeQuery(FdbTransaction transaction, FdbKeySelector begin, FdbKeySelector end, int limit, int targetBytes, FdbStreamingMode mode, bool snapshot, bool reverse)
		{
			this.Transaction = transaction;
			this.Begin = begin;
			this.End = end;
			this.Limit = limit;
			this.TargetBytes = targetBytes;
			this.Mode = mode;
			this.Snapshot = snapshot;
			this.Reverse = reverse;
		}

		/// <summary>Copy constructor</summary>
		private FdbRangeQuery(FdbRangeQuery query)
		{
			this.Transaction = query.Transaction;
			this.Begin = query.Begin;
			this.End = query.End;
			this.Limit = query.Limit;
			this.TargetBytes = query.TargetBytes;
			this.Mode = query.Mode;
			this.Snapshot = query.Snapshot;
			this.Reverse = query.Reverse;
		}

		#region Public Properties...

		/// <summary>Key selector describing the beginning of the range</summary>
		public FdbKeySelector Begin { get; private set; }

		/// <summary>Key selector describing the end of the range</summary>
		public FdbKeySelector End { get; private set; }

		/// <summary>Limit in number of rows to return</summary>
		public int Limit { get; private set; }

		/// <summary>Limit in number of bytes to return</summary>
		public int TargetBytes { get; private set; }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode Mode { get; private set; }

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		public bool Snapshot { get; private set; }

		/// <summary>Should the results returned in reverse order (from last key to first key)</summary>
		public bool Reverse { get; private set; }

		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		internal FdbTransaction Transaction { get; private set; }

		/// <summary>True if a row limit has been set; otherwise false.</summary>
		public bool HasLimit { get { return this.Limit > 0; } }

		#endregion

		#region Fluent API

		public FdbRangeQuery Take(int count)
		{
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Value cannot be less than zero");

			return new FdbRangeQuery(this)
			{
				Limit = count
			};
		}

		public FdbRangeQuery Reversed()
		{
			return new FdbRangeQuery(this)
			{
				Reverse = !this.Reverse
			};
		}

		public FdbRangeQuery WithTargetBytes(int bytes)
		{
			if (bytes < 0) throw new ArgumentOutOfRangeException("bytes", "Value cannot be less than zero");

			return new FdbRangeQuery(this)
			{
				TargetBytes = bytes
			};
		}

		public FdbRangeQuery WithMode(FdbStreamingMode mode)
		{
			if (!Enum.IsDefined(typeof(FdbStreamingMode), mode))
			{
				throw new ArgumentOutOfRangeException("mode", "Unsupported streaming mode");
			}

			return new FdbRangeQuery(this)
			{
				Mode = mode
			};
		}

		public FdbRangeQuery UseTransaction(FdbTransaction transaction)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			return new FdbRangeQuery(this)
			{
				Transaction = transaction
			};
		}

		#endregion

		#region Pseudo-LINQ

		private bool m_gotUsedOnce;

		public IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>> GetEnumerator()
		{
			if (m_gotUsedOnce) throw new InvalidOperationException("This query has already been executed once. Reusing the same query object would re-run the query on the server. If you need to data multiple times, you should call ToListAsync() one time, and then reuse this list using normal LINQ to Object operators.");
			m_gotUsedOnce = true;
			return new ResultIterator<KeyValuePair<Slice, Slice>>(this, x => x);
		}

		public Task<List<KeyValuePair<Slice, Slice>>> ToListAsync(CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ToListAsync(this, ct);
		}

		public IFdbAsyncEnumerable<T> Select<T>(Func<KeyValuePair<Slice, Slice>, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, lambda);
		}

		public IFdbAsyncEnumerable<T> Select<T>(Func<Slice, Slice, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, (kvp) => lambda(kvp.Key, kvp.Value));
		}

		public IFdbAsyncEnumerable<KeyValuePair<K, V>> Select<K, V>(Func<Slice, K> extractKey, Func<Slice, V> extractValue)
		{
			return FdbAsyncEnumerable.Select(this, (kvp) => new KeyValuePair<K, V>(extractKey(kvp.Key), extractValue(kvp.Value)));
		}

		public IFdbAsyncEnumerable<R> Select<K, V, R>(Func<Slice, K> extractKey, Func<Slice, V> extractValue, Func<K, V, R> transform)
		{
			return FdbAsyncEnumerable.Select(this, (kvp) => transform(extractKey(kvp.Key), extractValue(kvp.Value)));
		}

		public IFdbAsyncEnumerable<Slice> Keys()
		{
			return FdbAsyncEnumerable.Select(this, kvp => kvp.Key);
		}

		public IFdbAsyncEnumerable<T> Keys<T>(Func<Slice, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, kvp => lambda(kvp.Key));
		}

		public IFdbAsyncEnumerable<Slice> Values()
		{
			return FdbAsyncEnumerable.Select(this, kvp => kvp.Value);
		}

		public IFdbAsyncEnumerable<T> Values<T>(Func<Slice, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, kvp => lambda(kvp.Value));
		}

		public Task ForEachAsync(Action<KeyValuePair<Slice, Slice>> action, CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ForEachAsync(this, action, ct);
		}

		public Task ForEachAsync(Action<Slice, Slice> action, CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ForEachAsync(this, (kvp) => action(kvp.Key, kvp.Value), ct);
		}

		#endregion

		public IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>[]> Batched()
		{
			return new BatchQuery(this);
		}

		private sealed class BatchQuery : IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>[]>
		{

			private readonly FdbRangeQuery m_query;

			public BatchQuery(FdbRangeQuery query)
			{
				Contract.Requires(query != null);
				m_query = query;
			}

			public IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>[]> GetEnumerator()
			{
				return new FdbRangeQuery.PagingIterator(m_query);
			}

		}

		public override string ToString()
		{
			return "(" + this.Begin.ToString() + " ... " + this.End.ToString() + ")";
		}

	}

}
