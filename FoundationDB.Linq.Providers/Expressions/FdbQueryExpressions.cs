#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Linq.Expressions
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Helper class to construct Query Expressions</summary>
	public static class FdbQueryExpressions
	{

		/// <summary>Return a single result from the query</summary>
		[NotNull]
		public static FdbQuerySingleExpression<T, R> Single<T, R>([NotNull] FdbQuerySequenceExpression<T> source, string name, [NotNull] Expression<Func<IFdbAsyncEnumerable<T>, CancellationToken, Task<R>>> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			if (name == null) name = lambda.Name ?? "Lambda";

			return new FdbQuerySingleExpression<T, R>(source, name, lambda);
		}

		/// <summary>Return a sequence of results from the query</summary>
		[NotNull]
		public static FdbQueryAsyncEnumerableExpression<T> Sequence<T>([NotNull] IFdbAsyncEnumerable<T> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			return new FdbQueryAsyncEnumerableExpression<T>(source);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		[NotNull]
		public static FdbQueryRangeExpression Range(FdbKeySelectorPair range, FdbRangeOptions options = null)
		{
			return new FdbQueryRangeExpression(range, options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		[NotNull]
		public static FdbQueryRangeExpression Range(FdbKeySelector start, FdbKeySelector stop, FdbRangeOptions options = null)
		{
			return Range(new FdbKeySelectorPair(start, stop), options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		[NotNull]
		public static FdbQueryRangeExpression RangeStartsWith(Slice prefix, FdbRangeOptions options = null)
		{
			// starts_with('A') means ['A', B')
			return Range(FdbKeySelectorPair.StartsWith(prefix), options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		[NotNull]
		public static FdbQueryRangeExpression RangeStartsWith(IFdbTuple tuple, FdbRangeOptions options = null)
		{
			return Range(tuple.ToSelectorPair(), options);
		}

		/// <summary>Return the intersection between one of more sequences of results</summary>
		[NotNull]
		public static FdbQueryIntersectExpression<T> Intersect<T>(params FdbQuerySequenceExpression<T>[] expressions)
		{
			if (expressions == null) throw new ArgumentNullException("expressions");
			if (expressions.Length <= 1) throw new ArgumentException("There must be at least two sequences to perform an intersection");

			var type = expressions[0].Type;
			//TODO: check that all the other have the same type

			return new FdbQueryIntersectExpression<T>(expressions, null);
		}

		/// <summary>Return the union between one of more sequences of results</summary>
		[NotNull]
		public static FdbQueryUnionExpression<T> Union<T>(params FdbQuerySequenceExpression<T>[] expressions)
		{
			if (expressions == null) throw new ArgumentNullException("expressions");
			if (expressions.Length <= 1) throw new ArgumentException("There must be at least two sequences to perform an intersection");

			var type = expressions[0].Type;
			//TODO: check that all the other have the same type

			return new FdbQueryUnionExpression<T>(expressions, null);
		}

		/// <summary>Transform each elements of a sequence into a new sequence</summary>
		[NotNull]
		public static FdbQueryTransformExpression<T, R> Transform<T, R>([NotNull] FdbQuerySequenceExpression<T> source, [NotNull] Expression<Func<T, R>> transform)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (transform == null) throw new ArgumentNullException("transform");

			if (source.ElementType != typeof(T)) throw new ArgumentException(String.Format("Source sequence has type {0} that is not compatible with transform input type {1}", source.ElementType.Name, typeof(T).Name), "source");

			return new FdbQueryTransformExpression<T, R>(source, transform);
		}

		/// <summary>Filter out the elements of e sequence that do not match a predicate</summary>
		[NotNull]
		public static FdbQueryFilterExpression<T> Filter<T>([NotNull] FdbQuerySequenceExpression<T> source, [NotNull] Expression<Func<T, bool>> filter)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (filter == null) throw new ArgumentNullException("filter");

			if (source.ElementType != typeof(T)) throw new ArgumentException(String.Format("Source sequence has type {0} that is not compatible with filter input type {1}", source.ElementType.Name, typeof(T).Name), "source");

			return new FdbQueryFilterExpression<T>(source, filter);
		}

		/// <summary>Returns a human-readble explanation of a query that returns a single element</summary>
		[NotNull]
		public static string ExplainSingle<T>([NotNull] FdbQueryExpression<T> expression, CancellationToken ct)
		{
			if (expression == null) throw new ArgumentNullException("expression");
			if (expression.Shape != FdbQueryShape.Single) throw new InvalidOperationException("Invalid shape (single expected)");

			var expr = expression.CompileSingle();
			return expr.GetDebugView();
		}


		/// <summary>Returns a human-readble explanation of a query that returns a sequence of elements</summary>
		[NotNull]
		public static string ExplainSequence<T>([NotNull] FdbQuerySequenceExpression<T> expression)
		{
			if (expression == null) throw new ArgumentNullException("expression");
			if (expression.Shape != FdbQueryShape.Sequence) throw new InvalidOperationException("Invalid shape (sequence expected)");

			var expr = expression.CompileSequence();
			return expr.GetDebugView();
		}

	}

}
