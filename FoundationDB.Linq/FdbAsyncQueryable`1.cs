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
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq.Expressions;
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;

	public interface IFdbDatabaseQueryable : IFdbAsyncQueryable
	{

	}

	public static class FdbAsyncQueryable
	{

		public static IFdbDatabaseQueryable Query(this FdbDatabase db)
		{
			if (db == null) throw new ArgumentNullException("db");

			return new FdbAsyncQuery(db);
		}

		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> Range(this IFdbDatabaseQueryable query, FdbKeySelectorPair range)
		{
			if (query == null) throw new ArgumentNullException("query");

			var expr = FdbQueryExpressions.Range(range);

			return query.Provider.CreateSequenceQuery(expr);
		}

		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> RangeStartsWith(this IFdbDatabaseQueryable query, IFdbTuple prefix)
		{
			if (query == null) throw new ArgumentNullException("query");

			var expr = FdbQueryExpressions.RangeStartsWith(prefix);

			return query.Provider.CreateSequenceQuery(expr);
		}

		public static IFdbAsyncSequenceQueryable<R> Select<T, R>(this IFdbAsyncSequenceQueryable<T> query, Expression<Func<T, R>> selector)
		{
			if (query == null) throw new ArgumentNullException("query");
			if (selector == null) throw new ArgumentNullException("selector");

			var sourceExpr = query.Expression as FdbQuerySequenceExpression<T>;
			if (sourceExpr == null) throw new ArgumentException("query");

			var expr = FdbQueryExpressions.Transform(sourceExpr, selector);

			return query.Provider.CreateSequenceQuery<R>(expr);
		}

		public static IFdbAsyncSequenceQueryable<T> Where<T>(this IFdbAsyncSequenceQueryable<T> query, Expression<Func<T, bool>> predicate)
		{
			if (query == null) throw new ArgumentNullException("query");
			if (predicate == null) throw new ArgumentNullException("predicate");

			var sourceExpr = query.Expression as FdbQuerySequenceExpression<T>;
			if (sourceExpr == null) throw new ArgumentException("query");

			var expr = FdbQueryExpressions.Filter(sourceExpr, predicate);

			return query.Provider.CreateSequenceQuery<T>(expr);
		}

		public static IFdbAsyncQueryable<T> First<T>(this IFdbAsyncSequenceQueryable<T> query)
		{
			if (query == null) throw new ArgumentNullException("query");

			var method = typeof(FdbAsyncEnumerable).GetMethod("First").MakeGenericMethod(typeof(T));

			var expr = FdbQueryExpressions.Single(query.Expression, method);

			return query.Provider.CreateQuery<T>(expr);
		}

	}

}
