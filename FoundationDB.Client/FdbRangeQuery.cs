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
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Query describing an ongoing GetRange operation</summary>
	[DebuggerDisplay("Begin={Range.Start}, End={Range.Stop}, Limit={Limit}, Mode={Mode}, Reverse={Reverse}, Snapshot={Snapshot}")]
	public partial class FdbRangeQuery : IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>>
	{

		/// <summary>Construct a query with a set of initial settings</summary>
		internal FdbRangeQuery(IFdbReadTransaction transaction, FdbKeySelectorPair range, FdbRangeOptions options, bool snapshot)
		{
			this.Transaction = transaction;
			this.Range = range;
			this.Limit = options.Limit ?? 0;
			this.TargetBytes = options.TargetBytes ?? 0;
			this.Mode = options.Mode ?? FdbStreamingMode.Iterator;
			this.Snapshot = snapshot;
			this.Reverse = options.Reverse ?? false;
		}

		/// <summary>Copy constructor</summary>
		private FdbRangeQuery(FdbRangeQuery query)
		{
			this.Transaction = query.Transaction;
			this.Range = query.Range;
			this.Limit = query.Limit;
			this.TargetBytes = query.TargetBytes;
			this.Mode = query.Mode;
			this.Snapshot = query.Snapshot;
			this.Reverse = query.Reverse;
		}

		#region Public Properties...

		/// <summary>Key selector pair describing the beginning and end of the range that will be queried</summary>
		public FdbKeySelectorPair Range { get; private set; }

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
		internal IFdbReadTransaction Transaction { get; private set; }

		/// <summary>True if a row limit has been set; otherwise false.</summary>
		public bool HasLimit { get { return this.Limit > 0; } }

		#endregion

		#region Fluent API

		/// <summary>Only return up to a specific number of results</summary>
		/// <param name="count">Maximum number of results to return</param>
		/// <returns>A new query object that will only return up to <paramref name="count"/> results when executed</returns>
		public FdbRangeQuery Take(int count)
		{
			//REVIEW: this should maybe renamed to "Limit(..)" to align with FDB's naming convention, but at the same times it would not match with LINQ...
			// But, having both Take(..) and Limit(..) could be confusing...

			if (count < 0) throw new ArgumentOutOfRangeException("count", "Value cannot be less than zero");

			return new FdbRangeQuery(this)
			{
				Limit = count
			};
		}

		/// <summary>Reverse the order in which the results will be returned</summary>
		/// <returns>A new query object that will return the results in reverse order when executed</returns>
		/// <rremarks>Calling Reversed() on an already reversed query will cancel the effect, and the results will be returned in their natural order.</rremarks>
		public FdbRangeQuery Reversed()
		{
			return new FdbRangeQuery(this)
			{
				Reverse = !this.Reverse
			};
		}

		/// <summary>Use a specific target bytes size</summary>
		/// <param name="bytes"></param>
		/// <returns>A new query object that will use the specified target bytes size when executed</returns>
		public FdbRangeQuery WithTargetBytes(int bytes)
		{
			if (bytes < 0) throw new ArgumentOutOfRangeException("bytes", "Value cannot be less than zero");

			return new FdbRangeQuery(this)
			{
				TargetBytes = bytes
			};
		}

		/// <summary>Use a different Streaming Mode</summary>
		/// <param name="mode">Streaming mode to use when reading the results from the database</param>
		/// <returns>A new query object that will use the specified streaming mode when executed</returns>
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

		/// <summary>Force the query to use a specific transaction</summary>
		/// <param name="transaction">Transaction to use when executing this query</param>
		/// <returns>A new query object that will use the specified transaction when executed</returns>
		public FdbRangeQuery UseTransaction(IFdbReadTransaction transaction)
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

		public IAsyncEnumerator<KeyValuePair<Slice, Slice>> GetEnumerator()
		{
			return this.GetEnumerator(FdbAsyncMode.Default);
		}

		public IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>> GetEnumerator(FdbAsyncMode mode)
		{
			if (m_gotUsedOnce) throw new InvalidOperationException("This query has already been executed once. Reusing the same query object would re-run the query on the server. If you need to data multiple times, you should call ToListAsync() one time, and then reuse this list using normal LINQ to Object operators.");
			m_gotUsedOnce = true;

			return new ResultIterator<KeyValuePair<Slice, Slice>>(this, this.Transaction, TaskHelpers.Cache<KeyValuePair<Slice, Slice>>.Identity).GetEnumerator(mode);
		}

		/// <summary>Return a list of all the elements of the range results</summary>
		public Task<List<KeyValuePair<Slice, Slice>>> ToListAsync(CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ToListAsync(this, ct);
		}

		/// <summary>Return an array with all the elements of the range results</summary>
		public Task<KeyValuePair<Slice, Slice>[]> ToArrayAsync(CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ToArrayAsync(this, ct);
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		public IFdbAsyncEnumerable<T> Select<T>(Func<KeyValuePair<Slice, Slice>, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, lambda);
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		public IFdbAsyncEnumerable<T> Select<T>(Func<Slice, Slice, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, (kvp) => lambda(kvp.Key, kvp.Value));
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		public IFdbAsyncEnumerable<KeyValuePair<K, V>> Select<K, V>(Func<Slice, K> extractKey, Func<Slice, V> extractValue)
		{
			return FdbAsyncEnumerable.Select(this, (kvp) => new KeyValuePair<K, V>(extractKey(kvp.Key), extractValue(kvp.Value)));
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		public IFdbAsyncEnumerable<R> Select<K, V, R>(Func<Slice, K> extractKey, Func<Slice, V> extractValue, Func<K, V, R> transform)
		{
			return FdbAsyncEnumerable.Select(this, (kvp) => transform(extractKey(kvp.Key), extractValue(kvp.Value)));
		}

		/// <summary>Return true if the range query does not return any valid elements, or false if there was at least one result.</summary>
		/// <remarks>This is a convenience method that is there to help porting layer code from other languages. This is strictly equivalent to calling "!(await query.AnyAsync())".</remarks>
		public Task<bool> NoneAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize by using Limit = 1!

			var query = this;
			if (query.Limit != 1) query = query.Take(1);

			return FdbAsyncEnumerable.NoneAsync(query, ct);
		}

		/// <summary>Only return the keys out of range results</summary>
		public IFdbAsyncEnumerable<Slice> Keys()
		{
			return FdbAsyncEnumerable.Select(this, kvp => kvp.Key);
		}

		/// <summary>Apply a transformation on the keys of the range results</summary>
		public IFdbAsyncEnumerable<T> Keys<T>(Func<Slice, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, kvp => lambda(kvp.Key));
		}

		/// <summary>Only return the values out of range results</summary>
		public IFdbAsyncEnumerable<Slice> Values()
		{
			return FdbAsyncEnumerable.Select(this, kvp => kvp.Value);
		}

		/// <summary>Apply a transformation on the values of the range results</summary>
		public IFdbAsyncEnumerable<T> Values<T>(Func<Slice, T> lambda)
		{
			return FdbAsyncEnumerable.Select(this, kvp => lambda(kvp.Value));
		}

		/// <summary>Execute an action on each key/value pair of the range results</summary>
		public Task ForEachAsync(Action<KeyValuePair<Slice, Slice>> action, CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ForEachAsync(this, action, ct);
		}

		/// <summary>Execute an action on each key/value pair of the range results</summary>
		public Task ForEachAsync(Action<Slice, Slice> action, CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ForEachAsync(this, (kvp) => action(kvp.Key, kvp.Value), ct);
		}

		#endregion

		/// <summary>Return the results in batches, instead of one by one. This can helps improve performances in some specific scenarios.</summary>
		public IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>[]> Batched()
		{
			return new BatchQuery(this);
		}

		/// <summary>Wrapper class that will expose the underlying paging iterator</summary>
		private sealed class BatchQuery : IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>[]>
		{

			private readonly FdbRangeQuery m_query;

			public BatchQuery(FdbRangeQuery query)
			{
				Contract.Requires(query != null);
				m_query = query;
			}

			public IAsyncEnumerator<KeyValuePair<Slice, Slice>[]> GetEnumerator()
			{
				return this.GetEnumerator(FdbAsyncMode.Default);
			}

			public IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>[]> GetEnumerator(FdbAsyncMode mode)
			{
				return new FdbRangeQuery.PagingIterator(m_query, null).GetEnumerator(mode);
			}

		}

		/// <summary>Returns a printable version of the range query</summary>
		public override string ToString()
		{
			return String.Format("Range({0}, {1}, {2})", this.Range.ToString(), this.Limit.ToString(), this.Reverse ? "reverse" : "forward");
		}

	}

}
