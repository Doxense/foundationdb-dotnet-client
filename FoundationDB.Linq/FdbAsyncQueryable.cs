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

namespace FoundationDB.Linq
{
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq.Expressions;
	using FoundationDB.Linq.Utils;
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;

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

		/// <summary>Start a query on a database</summary>
		/// <param name="db">Source database</param>
		/// <returns>Query that will use this database as a source</returns>
		public static IFdbDatabaseQueryable Query(this FdbDatabase db)
		{
			if (db == null) throw new ArgumentNullException("db");

			return new FdbDatabaseQuery(db);
		}

		/// <summary>Return a range of keys</summary>
		/// <param name="query">Source database query</param>
		/// <param name="range">Pair of key selectors</param>
		/// <returns>Query that will return the keys from the specified <paramref name="range"/></returns>
		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> Range(this IFdbDatabaseQueryable query, FdbKeySelectorPair range)
		{
			if (query == null) throw new ArgumentNullException("query");

			var expr = FdbQueryExpressions.Range(range);

			return query.Provider.CreateSequenceQuery(expr);
		}

		/// <summary>Return all the keys that share a common prefix</summary>
		/// <param name="query">Source database query</param>
		/// <param name="prefix">Shared prefix</param>
		/// <returns>Query that will return the keys that share the specified <paramref name="prefix"/></returns>
		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> RangeStartsWith(this IFdbDatabaseQueryable query, IFdbTuple prefix)
		{
			if (query == null) throw new ArgumentNullException("query");

			var expr = FdbQueryExpressions.RangeStartsWith(prefix);

			return query.Provider.CreateSequenceQuery(expr);
		}

		#endregion

		#region Index Queries...

		public static IFdbIndexQueryable<TId, TValue> Query<TId, TValue>(this FdbIndex<TId, TValue> index, FdbDatabase db)
		{
			if (index == null) throw new ArgumentNullException("index");
			if (db == null) throw new ArgumentNullException("db");

			return new FdbIndexQuery<TId, TValue>(db, index);
		}

		public static IFdbAsyncSequenceQueryable<TId> Lookup<TId, TValue>(this IFdbIndexQueryable<TId, TValue> query, Expression<Func<TValue , bool>> predicate)
		{
			var expr = FdbQueryExpressions.Lookup(query.Index, predicate);

			return query.Provider.CreateSequenceQuery(expr);
		}

		#endregion

		/// <summary>Projects each element of a sequence query into a new form.</summary>
		public static IFdbAsyncSequenceQueryable<R> Select<T, R>(this IFdbAsyncSequenceQueryable<T> query, Expression<Func<T, R>> selector)
		{
			if (query == null) throw new ArgumentNullException("query");
			if (selector == null) throw new ArgumentNullException("selector");

			var sourceExpr = query.Expression as FdbQuerySequenceExpression<T>;
			if (sourceExpr == null) throw new ArgumentException("query");

			var expr = FdbQueryExpressions.Transform(sourceExpr, selector);

			return query.Provider.CreateSequenceQuery<R>(expr);
		}

		/// <summary>Filters a sequence query of values based on a predicate.</summary>
		public static IFdbAsyncSequenceQueryable<T> Where<T>(this IFdbAsyncSequenceQueryable<T> query, Expression<Func<T, bool>> predicate)
		{
			if (query == null) throw new ArgumentNullException("query");
			if (predicate == null) throw new ArgumentNullException("predicate");

			var sourceExpr = query.Expression as FdbQuerySequenceExpression<T>;
			if (sourceExpr == null) throw new ArgumentException("query");

			var expr = FdbQueryExpressions.Filter(sourceExpr, predicate);

			return query.Provider.CreateSequenceQuery<T>(expr);
		}

		public static IFdbAsyncEnumerable<T> ToAsyncEnumerable<T>(this IFdbAsyncSequenceQueryable<T> query)
		{
			if (query == null) throw new ArgumentNullException("query");

			var sequenceQuery = query as FdbAsyncSequenceQuery<T>;
			if (sequenceQuery == null) throw new ArgumentException("Source query type not supported", "query");

			return sequenceQuery.ToEnumerable();
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<int> CountAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException("query");

			var expr = FdbQueryExpressions.Single<T, int>(
				query.Expression,
				"CountAsync",
				(source, _ct) => source.CountAsync(_ct)
			);

			return query.Provider.CreateQuery<int>(expr).ExecuteSingle(ct);
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<T> FirstAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException("query");
			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<T>(ct);

			var expr = FdbQueryExpressions.Single<T, T>(
				query.Expression,
				"FirstAsync",
				(source, _ct) => source.FirstAsync(_ct)
			);

			return query.Provider.CreateQuery<T>(expr).ExecuteSingle(ct);
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException("query");
			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<T>(ct);

			var expr = FdbQueryExpressions.Single<T, T>(
				query.Expression,
				"FirstOrDefaultAsync",
				(source, _ct) => source.FirstOrDefaultAsync(_ct)
			);

			return query.Provider.CreateQuery<T>(expr).ExecuteSingle(ct);
		}

		public static Task<List<T>> ToListAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException("query");
			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<List<T>>(ct);

			return query.Provider.ExecuteAsync<List<T>>(query.Expression, ct);

		}

		public static Task<T[]> ToArrayAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default(CancellationToken))
		{
			if (query == null) throw new ArgumentNullException("query");
			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<T[]>(ct);

			return query.Provider.ExecuteAsync<T[]>(query.Expression, ct);
		}

	}

}
