#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Linq.Expressions
{
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Helper class to construct Query Expressions</summary>
	public static class FdbQueryExpressions
	{

		/// <summary>Return a single result from the query</summary>
		public static FdbQuerySingleExpression<T, R> Single<T, R>(FdbQuerySequenceExpression<T> source, string name, Expression<Func<IAsyncEnumerable<T>, CancellationToken, Task<R>>> lambda)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (lambda == null) throw new ArgumentNullException(nameof(lambda));

			if (name == null) name = lambda.Name ?? "Lambda";

			return new FdbQuerySingleExpression<T, R>(source, name, lambda);
		}

		/// <summary>Return a sequence of results from the query</summary>
		public static FdbQueryAsyncEnumerableExpression<T> Sequence<T>(IAsyncEnumerable<T> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			return new FdbQueryAsyncEnumerableExpression<T>(source);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		public static FdbQueryRangeExpression Range(KeySelectorPair range, FdbRangeOptions? options = null)
		{
			return new FdbQueryRangeExpression(range, options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		public static FdbQueryRangeExpression Range(KeySelector start, KeySelector stop, FdbRangeOptions? options = null)
		{
			return Range(new KeySelectorPair(start, stop), options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		public static FdbQueryRangeExpression RangeStartsWith(Slice prefix, FdbRangeOptions? options = null)
		{
			// starts_with('A') means ['A', B')
			return Range(KeySelectorPair.StartsWith(prefix), options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		[Obsolete]
		public static FdbQueryRangeExpression RangeStartsWith(IVarTuple tuple, FdbRangeOptions? options = null)
		{
			return RangeStartsWith(TuPack.Pack(tuple), options);
		}

		/// <summary>Return the intersection between one of more sequences of results</summary>
		public static FdbQueryIntersectExpression<T> Intersect<T>(params FdbQuerySequenceExpression<T>[] expressions)
		{
			if (expressions == null) throw new ArgumentNullException(nameof(expressions));
			if (expressions.Length <= 1) throw new ArgumentException("There must be at least two sequences to perform an intersection");

			var type = expressions[0].Type;
			//TODO: check that all the other have the same type

			return new FdbQueryIntersectExpression<T>(expressions, null);
		}

		/// <summary>Return the union between one of more sequences of results</summary>
		public static FdbQueryUnionExpression<T> Union<T>(params FdbQuerySequenceExpression<T>[] expressions)
		{
			if (expressions == null) throw new ArgumentNullException(nameof(expressions));
			if (expressions.Length <= 1) throw new ArgumentException("There must be at least two sequences to perform an intersection");

			var type = expressions[0].Type;
			//TODO: check that all the other have the same type

			return new FdbQueryUnionExpression<T>(expressions, null);
		}

		/// <summary>Transform each elements of a sequence into a new sequence</summary>
		public static FdbQueryTransformExpression<T, R> Transform<T, R>(FdbQuerySequenceExpression<T> source, Expression<Func<T, R>> transform)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (transform == null) throw new ArgumentNullException(nameof(transform));

			if (source.ElementType != typeof(T)) throw new ArgumentException($"Source sequence has type {source.ElementType.Name} that is not compatible with transform input type {typeof(T).Name}", nameof(source));

			return new FdbQueryTransformExpression<T, R>(source, transform);
		}

		/// <summary>Filter out the elements of e sequence that do not match a predicate</summary>
		public static FdbQueryFilterExpression<T> Filter<T>(FdbQuerySequenceExpression<T> source, Expression<Func<T, bool>> filter)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (filter == null) throw new ArgumentNullException(nameof(filter));

			if (source.ElementType != typeof(T)) throw new ArgumentException($"Source sequence has type {source.ElementType.Name} that is not compatible with filter input type {typeof(T).Name}", nameof(source));

			return new FdbQueryFilterExpression<T>(source, filter);
		}

		/// <summary>Returns a human-readable explanation of a query that returns a single element</summary>
		public static string ExplainSingle<T>(FdbQueryExpression<T> expression, CancellationToken ct)
		{
			if (expression == null) throw new ArgumentNullException(nameof(expression));
			if (expression.Shape != FdbQueryShape.Single) throw new InvalidOperationException("Invalid shape (single expected)");

			var expr = expression.CompileSingle();
			return expr.GetDebugView();
		}


		/// <summary>Returns a human-readable explanation of a query that returns a sequence of elements</summary>
		public static string ExplainSequence<T>(FdbQuerySequenceExpression<T> expression)
		{
			if (expression == null) throw new ArgumentNullException(nameof(expression));
			if (expression.Shape != FdbQueryShape.Sequence) throw new InvalidOperationException("Invalid shape (sequence expected)");

			var expr = expression.CompileSequence();
			return expr.GetDebugView();
		}

	}

}
