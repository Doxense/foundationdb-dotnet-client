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
	public partial class FdbRangeQuery<T> : IFdbAsyncEnumerable<T>
	{

		/// <summary>Construct a query with a set of initial settings</summary>
		internal FdbRangeQuery(IFdbReadTransaction transaction, FdbKeySelectorPair range, Func<KeyValuePair<Slice, Slice>, T> transform, bool snapshot, FdbRangeOptions options)
		{
			this.Transaction = transaction;
			this.Range = range;
			this.Transform = transform;
			this.Snapshot = snapshot;
			this.Options = options ?? new FdbRangeOptions();
		}

		private FdbRangeQuery(FdbRangeQuery<T> query, FdbRangeOptions options)
		{
			this.Transaction = query.Transaction;
			this.Range = query.Range;
			this.Transform = query.Transform;
			this.Options = options;
		}

		#region Public Properties...

		/// <summary>Key selector pair describing the beginning and end of the range that will be queried</summary>
		public FdbKeySelectorPair Range { get; private set; }

		/// <summary>Stores all the settings for this range query</summary>
		internal FdbRangeOptions Options { get; private set; }

		/// <summary>Limit in number of rows to return</summary>
		public int Limit { get { return this.Options.Limit ?? 0; } }

		/// <summary>Limit in number of bytes to return</summary>
		public int TargetBytes { get { return this.Options.TargetBytes ?? 0; } }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode Mode { get { return this.Options.Mode ?? FdbStreamingMode.Iterator; } }

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		public bool Snapshot { get; private set; }

		/// <summary>Should the results returned in reverse order (from last key to first key)</summary>
		public bool Reverse { get { return this.Options.Reverse ?? false; } }

		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		internal IFdbReadTransaction Transaction { get; private set; }

		internal Func<KeyValuePair<Slice, Slice>, T> Transform { get; private set; }

		#endregion

		#region Fluent API

		/// <summary>Only return up to a specific number of results</summary>
		/// <param name="count">Maximum number of results to return</param>
		/// <returns>A new query object that will only return up to <paramref name="count"/> results when executed</returns>
		public FdbRangeQuery<T> Take(int count)
		{
			//REVIEW: this should maybe renamed to "Limit(..)" to align with FDB's naming convention, but at the same times it would not match with LINQ...
			// But, having both Take(..) and Limit(..) could be confusing...

			if (count < 0) throw new ArgumentOutOfRangeException("count", "Value cannot be less than zero");

			if (this.Options.Limit == count)
			{
				return this;
			}

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { Limit = count }
			);
		}

		/// <summary>Reverse the order in which the results will be returned</summary>
		/// <returns>A new query object that will return the results in reverse order when executed</returns>
		/// <rremarks>Calling Reversed() on an already reversed query will cancel the effect, and the results will be returned in their natural order.</rremarks>
		public FdbRangeQuery<T> Reversed()
		{
			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { Reverse = !this.Reverse }
			);
		}

		/// <summary>Use a specific target bytes size</summary>
		/// <param name="bytes"></param>
		/// <returns>A new query object that will use the specified target bytes size when executed</returns>
		public FdbRangeQuery<T> WithTargetBytes(int bytes)
		{
			if (bytes < 0) throw new ArgumentOutOfRangeException("bytes", "Value cannot be less than zero");

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { TargetBytes = bytes }
			);
		}

		/// <summary>Use a different Streaming Mode</summary>
		/// <param name="mode">Streaming mode to use when reading the results from the database</param>
		/// <returns>A new query object that will use the specified streaming mode when executed</returns>
		public FdbRangeQuery<T> WithMode(FdbStreamingMode mode)
		{
			if (!Enum.IsDefined(typeof(FdbStreamingMode), mode))
			{
				throw new ArgumentOutOfRangeException("mode", "Unsupported streaming mode");
			}

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { Mode = mode }
			);
		}

		/// <summary>Force the query to use a specific transaction</summary>
		/// <param name="transaction">Transaction to use when executing this query</param>
		/// <returns>A new query object that will use the specified transaction when executed</returns>
		public FdbRangeQuery<T> UseTransaction(IFdbReadTransaction transaction)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			return new FdbRangeQuery<T>(
				transaction,
				this.Range,
				this.Transform,
				this.Snapshot,
				new FdbRangeOptions(this.Options)
			);
		}

		#endregion

		#region Pseudo-LINQ

		private bool m_gotUsedOnce;

		public IAsyncEnumerator<T> GetEnumerator()
		{
			return this.GetEnumerator(FdbAsyncMode.Default);
		}

		public IFdbAsyncEnumerator<T> GetEnumerator(FdbAsyncMode mode)
		{
			//REVIEW: is this really a good thing ? this prohibit use a cached query for frequent queries (for uncacheable, fast changing data)
			if (m_gotUsedOnce) throw new InvalidOperationException("This query has already been executed once. Reusing the same query object would re-run the query on the server. If you need to data multiple times, you should call ToListAsync() one time, and then reuse this list using normal LINQ to Object operators.");
			m_gotUsedOnce = true;

			return new ResultIterator(this, this.Transaction, this.Transform).GetEnumerator(mode);
		}

		/// <summary>Return a list of all the elements of the range results</summary>
		public Task<List<T>> ToListAsync(CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ToListAsync(this, ct);
		}

		/// <summary>Return an array with all the elements of the range results</summary>
		public Task<T[]> ToArrayAsync(CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ToArrayAsync(this, ct);
		}

		internal FdbRangeQuery<R> Map<R>(Func<KeyValuePair<Slice, Slice>, R> transform)
		{
			return new FdbRangeQuery<R>(
				this.Transaction,
				this.Range,
				transform,
				this.Snapshot,
				new FdbRangeOptions(this.Options)
			);
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		public FdbRangeQuery<R> Select<R>(Func<T, R> lambda)
		{
			// note: avoid storing the query in the scope by storing the transform locally so that only 'f' and 'lambda' are kept alive
			var f = this.Transform;
			return this.Map<R>((x) => lambda(f(x)));
		}

		/// <summary>Filters the range results based on a predicate.</summary>
		/// <remarks>Caution: filtering occurs on the client side !</remarks>
		public IFdbAsyncEnumerable<T> Where(Func<T, bool> predicate)
		{
			return FdbAsyncEnumerable.Where(this, predicate);
		}

		public Task<T> FirstOrDefaultAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize this by passing Limit=1
			return HeadAsync(single: false, orDefault: true, ct: ct);
		}

		public Task<T> FirstAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize this by passing Limit=1
			return HeadAsync(single: false, orDefault: false, ct: ct);
		}

		public Task<T> LastOrDefaultAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize by reversing the current query and calling FirstOrDefault !
			return this.Reversed().HeadAsync(single:false, orDefault:true, ct: ct);
		}

		public Task<T> LastAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize this by reversing the current query and calling First !
			return this.Reversed().HeadAsync(single: false, orDefault:false, ct: ct);
		}

		public Task<T> SingleOrDefaultAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: true, ct: ct);
		}

		public Task<T> SingleAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: false, ct: ct);
		}

		/// <summary>Return true if the range query returns at least one element, or false if there was no result.</summary>
		public Task<bool> AnyAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize this by using Limit = 1
			return AnyOrNoneAsync(true, ct);
		}

		/// <summary>Return true if the range query does not return any valid elements, or false if there was at least one result.</summary>
		/// <remarks>This is a convenience method that is there to help porting layer code from other languages. This is strictly equivalent to calling "!(await query.AnyAsync())".</remarks>
		public Task<bool> NoneAsync(CancellationToken ct = default(CancellationToken))
		{
			// we can optimize this by using Limit = 1
			return AnyOrNoneAsync(false, ct);
		}

		/// <summary>Execute an action on each key/value pair of the range results</summary>
		public Task ForEachAsync(Action<T> action, CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ForEachAsync(this, action, ct);
		}

		internal async Task<T> HeadAsync(bool single, bool orDefault, CancellationToken ct)
		{
			// Optimized code path for First/Last/Single variants where we can be smart and only ask for 1 or 2 results from the db

			ct.ThrowIfCancellationRequested();

			// we can use the EXACT streaming mode with Limit = 1|2, and it will work if TargetBytes is 0
			if (this.TargetBytes != 0 || (this.Mode != FdbStreamingMode.Iterator && this.Mode != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				return await FdbAsyncEnumerable.Head(this, single, orDefault, ct).ConfigureAwait(false);
			}

			var options = new FdbRangeOptions()
			{
				Limit = single ? 2 : 1,
				TargetBytes = 0,
				Mode = FdbStreamingMode.Exact,
				Reverse = this.Reverse
			};

			var tr = this.Snapshot ? this.Transaction.ToSnapshotTransaction() : this.Transaction;
			var results = await tr.GetRangeAsync(this.Range, options, 0, ct).ConfigureAwait(false);

			if (results.Chunk.Length == 0)
			{ // no result
				if (!orDefault) throw new InvalidOperationException("The range was empty");
				return default(T);
			}

			if (single && results.Chunk.Length > 1)
			{ // there was more than one result
				throw new InvalidOperationException("The range contained more than one element");
			}

			// we have a result
			return this.Transform(results.Chunk[0]);
		}

		internal async Task<bool> AnyOrNoneAsync(bool any, CancellationToken ct)
		{
			// Optimized code path for Any/None where we can be smart and only ask for 1 from the db

			ct.ThrowIfCancellationRequested();

			// we can use the EXACT streaming mode with Limit = 1, and it will work if TargetBytes is 0
			if (this.TargetBytes != 0 || (this.Mode != FdbStreamingMode.Iterator && this.Mode != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				if (any)
					return await FdbAsyncEnumerable.AnyAsync(this, ct);
				else
					return await FdbAsyncEnumerable.NoneAsync(this, ct);
			}

			var options = new FdbRangeOptions()
			{
				Limit = 1,
				TargetBytes = 0,
				Mode = FdbStreamingMode.Exact,
				Reverse = this.Reverse
			};

			var tr = this.Snapshot ? this.Transaction.ToSnapshotTransaction() : this.Transaction;
			var results = await tr.GetRangeAsync(this.Range, options, 0, ct).ConfigureAwait(false);

			return any ? results.Chunk.Length > 0 : results.Chunk.Length == 0;
		}

		#endregion

		/// <summary>Returns a printable version of the range query</summary>
		public override string ToString()
		{
			return String.Format("Range({0}, {1}, {2})", this.Range.ToString(), this.Limit.ToString(), this.Reverse ? "reverse" : "forward");
		}

	}

	public static class FdbRangeQueryExtensions
	{

		public static FdbRangeQuery<K> Keys<K, V>(this FdbRangeQuery<KeyValuePair<K, V>> query)
		{
			if (query == null) throw new ArgumentNullException("query");

			var f = query.Transform;
			return query.Map<K>((x) => f(x).Key);
		}

		public static FdbRangeQuery<R> Keys<K, V, R>(this FdbRangeQuery<KeyValuePair<K, V>> query, Func<K, R> transform)
		{
			if (query == null) throw new ArgumentNullException("query");
			if (transform == null) throw new ArgumentNullException("transform");

			var f = query.Transform;
			return query.Map<R>((x) => transform(f(x).Key));
		}

		public static FdbRangeQuery<V> Values<K, V>(this FdbRangeQuery<KeyValuePair<K, V>> query)
		{
			if (query == null) throw new ArgumentNullException("query");

			var f = query.Transform;
			return query.Map<V>((x) => f(x).Value);
		}

		public static FdbRangeQuery<R> Values<K, V, R>(this FdbRangeQuery<KeyValuePair<K, V>> query, Func<V, R> transform)
		{
			if (query == null) throw new ArgumentNullException("query");
			if (transform == null) throw new ArgumentNullException("transform");

			var f = query.Transform;
			return query.Map<R>((x) => transform(f(x).Value));
		}

	}

}
