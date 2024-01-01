#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Linq
{
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Linq;
	using FoundationDB.Client;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Linq.Expressions;
	using FoundationDB.Linq.Providers;

	/// <summary>Extensions methods that help create a query expression tree</summary>
	public static class FdbAsyncQueryable
	{

		internal static Task<T> ExecuteSingle<T>(this IFdbAsyncQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			return query.Provider.ExecuteAsync<T>(query.Expression, ct);
		}

		internal static Task<T> ExecuteSequence<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			return query.Provider.ExecuteAsync<T>(query.Expression, ct);
		}

		#region Database Queries...

		/// <summary>Start a query on a transaction</summary>
		/// <param name="tr">Source transaction</param>
		/// <returns>Query that will use this transaction as a source</returns>
		public static IFdbTransactionQueryable Query(this IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			return new FdbTransactionQuery(tr);
		}

		/// <summary>Return a range of keys</summary>
		/// <param name="query">Source database query</param>
		/// <param name="range">Pair of key selectors</param>
		/// <returns>Query that will return the keys from the specified <paramref name="range"/></returns>
		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> Range(this IFdbTransactionQueryable query, KeySelectorPair range)
		{
			if (query == null) throw new ArgumentNullException(nameof(query));

			var expr = FdbQueryExpressions.Range(range);

			return query.Provider.CreateSequenceQuery(expr);
		}

		/// <summary>Return all the keys that share a common prefix</summary>
		/// <param name="query">Source database query</param>
		/// <param name="prefix">Shared prefix</param>
		/// <returns>Query that will return the keys that share the specified <paramref name="prefix"/></returns>
		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> RangeStartsWith(this IFdbTransactionQueryable query, Slice prefix)
		{
			if (query == null) throw new ArgumentNullException(nameof(query));

			var expr = FdbQueryExpressions.RangeStartsWith(prefix);

			return query.Provider.CreateSequenceQuery(expr);
		}

		#endregion

		#region Index Queries...

		/// <summary>Creates a new query on this index</summary>
		public static IFdbIndexQueryable<TId, TValue> Query<TId, TValue>(this FdbIndex<TId, TValue> index, IFdbDatabase db)
		{
			if (index == null) throw new ArgumentNullException(nameof(index));
			if (db == null) throw new ArgumentNullException(nameof(db));

			return new FdbIndexQuery<TId, TValue>(db, index);
		}

		/// <summary>Creates a new query that will lookup specific values on this index</summary>
		public static IFdbAsyncSequenceQueryable<TId> Lookup<TId, TValue>(this IFdbIndexQueryable<TId, TValue> query, Expression<Func<TValue , bool>> predicate)
		{
			var expr = FdbQueryIndexLookupExpression<TId, TValue>.Lookup(query.Index, predicate);

			return query.Provider.CreateSequenceQuery(expr);
		}

		#endregion

		/// <summary>Projects each element of a sequence query into a new form.</summary>
		public static IFdbAsyncSequenceQueryable<R> Select<T, R>(this IFdbAsyncSequenceQueryable<T> query, Expression<Func<T, R>> selector)
		{
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (selector == null) throw new ArgumentNullException(nameof(selector));

			var sourceExpr = query.Expression as FdbQuerySequenceExpression<T>;
			if (sourceExpr == null) throw new ArgumentException("query");

			var expr = FdbQueryExpressions.Transform(sourceExpr, selector);

			return query.Provider.CreateSequenceQuery<R>(expr);
		}

		/// <summary>Filters a sequence query of values based on a predicate.</summary>
		public static IFdbAsyncSequenceQueryable<T> Where<T>(this IFdbAsyncSequenceQueryable<T> query, Expression<Func<T, bool>> predicate)
		{
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (predicate == null) throw new ArgumentNullException(nameof(predicate));

			var sourceExpr = query.Expression as FdbQuerySequenceExpression<T>;
			if (sourceExpr == null) throw new ArgumentException("query");

			var expr = FdbQueryExpressions.Filter(sourceExpr, predicate);

			return query.Provider.CreateSequenceQuery<T>(expr);
		}

		/// <summary>Returns an async sequence that would return the results of this query as they arrive.</summary>
		public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IFdbAsyncSequenceQueryable<T> query)
		{
			if (query == null) throw new ArgumentNullException(nameof(query));

			var sequenceQuery = query as FdbAsyncSequenceQuery<T>;
			if (sequenceQuery == null) throw new ArgumentException("Source query type not supported", nameof(query));

			return sequenceQuery.ToEnumerable();
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<int> CountAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException(nameof(query));

			var expr = FdbQueryExpressions.Single<T, int>(
				(FdbQuerySequenceExpression<T>)query.Expression,
				"CountAsync",
				(source, _ct) => source.CountAsync(_ct)
			);

			return query.Provider.CreateQuery<int>(expr).ExecuteSingle(ct);
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<T> FirstAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (ct.IsCancellationRequested) return Task.FromCanceled<T>(ct);

			var expr = FdbQueryExpressions.Single<T, T>(
				(FdbQuerySequenceExpression<T>)query.Expression,
				"FirstAsync",
				(source, _ct) => source.FirstAsync(_ct)
			);

			return query.Provider.CreateQuery<T>(expr).ExecuteSingle(ct);
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (ct.IsCancellationRequested) return Task.FromCanceled<T>(ct);

			var expr = FdbQueryExpressions.Single<T, T>(
				(FdbQuerySequenceExpression<T>)query.Expression,
				"FirstOrDefaultAsync",
				(source, _ct) => source.FirstOrDefaultAsync(_ct)
			);

			return query.Provider.CreateQuery<T>(expr).ExecuteSingle(ct);
		}

		/// <summary>Immediately executes a sequence query and return a list of all the results once it has completed.</summary>
		public static Task<List<T>> ToListAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (ct.IsCancellationRequested) return Task.FromCanceled<List<T>>(ct);

			return query.Provider.ExecuteAsync<List<T>>(query.Expression, ct);

		}

		/// <summary>Immediately executes a sequence query and return an array of all the results once it has completed.</summary>
		public static Task<T[]> ToArrayAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (ct.IsCancellationRequested) return Task.FromCanceled<T[]>(ct);

			return query.Provider.ExecuteAsync<T[]>(query.Expression, ct);
		}

	}

}
