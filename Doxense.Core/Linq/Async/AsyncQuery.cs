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

namespace SnowBank.Linq
{
	using System;
	using System.Buffers;
	using System.Collections.Immutable;
	using System.Numerics;
	using Doxense.Linq;
	using SnowBank.Linq.Async.Expressions;
	using SnowBank.Linq.Async.Iterators;
	using Doxense.Threading.Tasks;

	/// <summary>Provides a set of static methods for querying objects that implement <see cref="IAsyncEnumerable{T}"/>.</summary>
	[PublicAPI]
	public static partial class AsyncQuery
	{
		// Welcome to the wonderful world of the Monads!

		#region Entering the Monad...

		/// <summary>Returns an empty async sequence</summary>
		[Pure]
		public static IAsyncLinqQuery<T> Empty<T>()
		{
			return EmptyQuery<T>.Default;
		}

		/// <summary>Returns an async sequence with a single element, which is a constant</summary>
		[Pure]
		public static IAsyncLinqQuery<T> Singleton<T>(T value, CancellationToken ct = default)
		{
			//note: we can't call this method Single<T>(T), because then Single<T>(Func<T>) would be ambiguous with Single<Func<T>>(T)
			return new EnumerableSequence<T>([ value ], ct);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="selector">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="selector"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<T> Single<T>(Func<T> selector, CancellationToken ct = default)
		{
			Contract.NotNull(selector);
			return new SingletonQuery<T>(selector, ct);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="selector">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="selector"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<T> Single<T>(Func<CancellationToken, Task<T>> selector, CancellationToken ct = default)
		{
			Contract.NotNull(selector);
			return new SingletonQuery<T>(selector, ct);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="selector">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="selector"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<T> Single<T>(Func<Task<T>> selector, CancellationToken ct = default)
		{
			Contract.NotNull(selector);
			return new SingletonQuery<T>(selector, ct);
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<T> ToAsyncQuery<T>(this IEnumerable<T> source, CancellationToken ct)
		{
			Contract.NotNull(source);

			return new EnumerableSequence<T>(source, ct);
		}

		/// <summary>Wraps an async lambda into an async sequence that will return the result of the lambda</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<T> FromTask<T>(Func<CancellationToken, Task<T>> asyncLambda, CancellationToken ct)
		{
			//TODO: create a custom iterator for this ?
			return ToAsyncQuery([ asyncLambda ], ct).Select((x, cancel) => x(cancel));
		}

		#endregion

		#region Staying in the Monad...

		#region SelectMany...

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.SelectMany(selector);
			}

			return SelectManyImpl(source, selector);
		}

		internal static SelectManyAsyncIterator<TSource, TResult> SelectManyImpl<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			return new(source, selector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IAsyncEnumerable<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.SelectMany(selector);
			}

			return Flatten(source, new AsyncTransformExpression<TSource, IAsyncEnumerable<TResult>>(selector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncQuery{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IAsyncQuery<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.SelectMany(selector);
			}

			return Flatten(source, new AsyncTransformExpression<TSource, IAsyncQuery<TResult>>(selector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.SelectMany(selector);
			}

			return SelectManyImpl(source, selector);
		}

		internal static IAsyncLinqQuery<TResult> SelectManyImpl<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> selector)
		{
			return Flatten(source, new AsyncTransformExpression<TSource,IEnumerable<TResult>>(selector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.SelectMany(collectionSelector, resultSelector);
			}

			return SelectManyImpl(source, collectionSelector, resultSelector);
		}

		internal static IAsyncLinqQuery<TResult> SelectManyImpl<TSource, TCollection, TResult>(IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			return Flatten(source, new(collectionSelector), resultSelector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.SelectMany(collectionSelector, resultSelector);
			}

			return SelectManyImpl(source, collectionSelector, resultSelector);
		}

		internal static IAsyncLinqQuery<TResult> SelectManyImpl<TSource, TCollection, TResult>(IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			return Flatten(source, new(collectionSelector), resultSelector);
		}

		#endregion

		#region Select...

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, TResult> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.Select(selector);
			}

			return SelectImpl(source, selector);
		}

		internal static SelectAsyncIterator<TSource,TResult> SelectImpl<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, TResult> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			return new(source, selector);
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, int, TResult> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.Select(selector);
			}

			return SelectImpl(source, selector);
		}

		internal static IAsyncLinqQuery<TResult> SelectImpl<TResult, TSource>(IAsyncQuery<TSource> source, Func<TSource, int, TResult> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!
			return Defer(() =>
			{
				int index = -1;
				return source.Select((item) => selector(item, checked(++index)));

			}, source.Cancellation);
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
#if NET9_0_OR_GREATER
		[OverloadResolutionPriority(1)]
#endif
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.Select(selector);
			}

			return SelectImpl(source, selector);
		}

		internal static SelectTaskAsyncIterator<TSource,TResult> SelectImpl<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<TResult>> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			return new(source, selector);
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, int, CancellationToken, Task<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.Select(selector);
			}

			return SelectImpl(source, selector);
		}

		internal static IAsyncLinqQuery<TResult> SelectImpl<TResult, TSource>(IAsyncQuery<TSource> source, Func<TSource, int, CancellationToken, Task<TResult>> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!
			return Defer(() =>
			{
				int index = -1;
				return source.Select((item, ct) => selector(item, checked(++index), ct));

			}, source.Cancellation);
		}

		#endregion

		#region Where...

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<TResult> iterator)
			{
				return iterator.Where(predicate);
			}

			return WhereImpl(source, predicate);
		}

		internal static WhereAsyncIterator<TResult> WhereImpl<TResult>(IAsyncQuery<TResult> source, Func<TResult, bool> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			return new(source, predicate);
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, int, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<TResult> iterator)
			{
				return iterator.Where(predicate);
			}

			return WhereImpl(source, predicate);
		}

		internal static IAsyncLinqQuery<TResult> WhereImpl<TResult>(IAsyncQuery<TResult> source, Func<TResult, int, bool> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!

			return Defer(() =>
			{
				int index = -1;
				return source.Where((item) => predicate(item, checked(++index)));
			}, source.Cancellation);
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<TResult> iterator)
			{
				return iterator.Where(predicate);
			}

			return WhereImpl(source, predicate);
		}

		internal static WhereExpressionAsyncIterator<TResult> WhereImpl<TResult>(IAsyncQuery<TResult> source, Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			return new(source, new(predicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, int, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<TResult> iterator)
			{
				return iterator.Where(predicate);
			}

			return WhereImpl(source, predicate);
		}

		internal static IAsyncLinqQuery<TResult> WhereImpl<TResult>(IAsyncQuery<TResult> source, Func<TResult, int, CancellationToken, Task<bool>> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!

			return Defer(() =>
			{
				int index = -1;
				return source.Where((item, ct) => predicate(item, checked(++index), ct));
			}, source.Cancellation);
		}

		#endregion

		#region Take...

		/// <summary>Returns a specified number of contiguous elements from the start of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Take<TSource>(this IAsyncQuery<TSource> source, int count)
		{
			Contract.NotNull(source);
			Contract.Positive(count, "Count cannot be less than zero");

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.Take(count);
			}

			return TakeImpl(source, count);
		}

		internal static WhereSelectExpressionAsyncIterator<TResult, TResult> TakeImpl<TResult>(IAsyncQuery<TResult> source, int limit)
		{
			Contract.Debug.Requires(source != null && limit >= 0);

			return new(source, filter: null, transform: new(), limit: limit, offset: null);
		}

		#endregion

		#region TakeWhile...

		/// <summary>Returns elements from an async sequence as long as a specified condition is true, and then skips the remaining elements.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> TakeWhile<TSource>(this IAsyncQuery<TSource> source, Func<TSource, bool> condition)
		{
			Contract.NotNull(source);
			Contract.NotNull(condition);

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.TakeWhile(condition);
			}

			return TakeWhileImpl(source, condition);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> TakeWhile<TSource>(this IAsyncQuery<TSource> source, Func<TSource, bool> condition, out QueryStatistics<bool> stopped)
		{
			Contract.NotNull(source);
			Contract.NotNull(condition);

			var signal = new QueryStatistics<bool>(false);
			stopped = signal;

			// to trigger the signal, we just intercept the condition returning false (which only happen once!)
			bool Wrapped(TSource x)
			{
				if (condition(x)) return true;
				signal.Update(true);
				return false;
			}

			return TakeWhile(source, Wrapped);
		}

		internal static TakeWhileAsyncIterator<TResult> TakeWhileImpl<TResult>(IAsyncQuery<TResult> source, Func<TResult, bool> condition)
		{
			return new(source, condition);
		}

		#endregion

		#region Skip...

		/// <summary>Skips the first elements of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Skip<TSource>(this IAsyncQuery<TSource> source, int count)
		{
			Contract.NotNull(source);
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be less than zero");

			if (source is IAsyncLinqQuery<TSource> iterator)
			{
				return iterator.Skip(count);
			}

			return SkipImpl(source, count);
		}

		internal static WhereSelectExpressionAsyncIterator<TResult, TResult> SkipImpl<TResult>(IAsyncQuery<TResult> source, int offset)
		{
			return new(source, filter: null, transform: new(), limit: null, offset: offset);
		}

		#endregion

		#region SelectAsync

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectAsync<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<TResult>> asyncSelector, ParallelAsyncQueryOptions? options = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncSelector);

			return new ParallelSelectAsyncIterator<TSource, TResult>(source, asyncSelector, options ?? new ParallelAsyncQueryOptions());
		}

		/// <summary>Always prefetch the next item from the inner sequence.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <returns>Sequence that prefetch the next item, when outputting the current item.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid pre-fetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Prefetch<TSource>(this IAsyncQuery<TSource> source)
		{
			Contract.NotNull(source);

			return new PrefetchingAsyncIterator<TSource>(source, 1);
		}

		/// <summary>Always prefetch the next item from the inner sequence.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <returns>Sequence that prefetch the next item, when outputting the current item.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid pre-fetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Prefetch<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			return new PrefetchingAsyncIterator<TSource>(source.ToAsyncQuery(ct), 1);
		}
		/// <summary>Prefetch a certain number of items from the inner sequence, before outputting the results one by one.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <param name="prefetchCount">Maximum number of items to buffer from the source before they are consumed by the rest of the query.</param>
		/// <returns>Sequence that returns items from a buffer of pre-fetched list.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid pre-fetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Prefetch<TSource>(this IAsyncQuery<TSource> source, int prefetchCount)
		{
			Contract.NotNull(source);
			if (prefetchCount <= 0) throw new ArgumentOutOfRangeException(nameof(prefetchCount), prefetchCount, "Prefetch count must be at least one.");

			return new PrefetchingAsyncIterator<TSource>(source, prefetchCount);
		}

		/// <summary>Buffers the items of a bursty sequence, into a sequence of variable-sized arrays made up of items that where produced in a very short timespan.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence, that produces bursts of items, produced from the same page of results, before reading the next page.</param>
		/// <param name="maxWindowSize">Maximum number of items to return in a single window. If more items arrive at the same time, a new window will be opened with the rest of the items.</param>
		/// <returns>Sequence of batches, where all the items of a single batch arrived at the same time. A batch is closed once the next call to MoveNext() on the inner sequence does not complete immediately. Batches can be smaller than <paramref name="maxWindowSize"/>.</returns>
		/// <remarks>
		/// This should only be called on bursty asynchronous sequences, and when you want to process items in batches, without incurring the cost of latency between two pages of results.
		/// You should avoid using this operator on sequences where each call to MoveNext() is asynchronous, since it would only produce batchs with only a single item.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource[]> Window<TSource>(this IAsyncQuery<TSource> source, int maxWindowSize)
		{
			Contract.NotNull(source);
			if (maxWindowSize <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(maxWindowSize), maxWindowSize, "Window size must be at least one.");

			return new WindowingAsyncIterator<TSource>(source, maxWindowSize);
		}

		/// <summary>Buffers the items of a source sequence, and outputs a sequence of fixed-sized arrays.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that will be cut into chunks containing at most <paramref name="batchSize"/> items.</param>
		/// <param name="batchSize">Number of items per batch. The last batch may contain less items, but should never be empty.</param>
		/// <returns>Sequence of arrays of size <paramref name="batchSize"/>, except the last batch which can have less items.</returns>
		/// <remarks>
		/// This operator does not care about the latency of each item, and will always try to fill each batch completely, before outputting a result.
		/// If you are working on an inner sequence that is bursty in nature, where items arrives in waves, you should use <see cref="Window{TSource}"/> which attempts to minimize the latency by outputting incomplete batches if needed.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource[]> Batch<TSource>(this IAsyncQuery<TSource> source, int batchSize)
		{
			Contract.NotNull(source);
			if (batchSize <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be at least one.");

			return new BatchingAsyncIterator<TSource>(source, batchSize);
		}

		#endregion

		#region Distinct...

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Distinct<TSource>(this IAsyncQuery<TSource> source, IEqualityComparer<TSource>? comparer = null)
		{
			Contract.NotNull(source);
			return new DistinctAsyncIterator<TSource>(source, comparer ?? EqualityComparer<TSource>.Default);
		}

		#endregion

		#region OrderBy...

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> OrderBy<TSource, TKey>(this IAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			return new OrderedSequence<TSource, TKey>(source, keySelector, comparer ?? Comparer<TKey>.Default, descending: false, parent: null);
		}

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> OrderByDescending<TSource, TKey>(this IAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			return new OrderedSequence<TSource, TKey>(source, keySelector, comparer ?? Comparer<TKey>.Default, descending: true, parent: null);
		}

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> ThenBy<TSource, TKey>(this IOrderedAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			return source.CreateOrderedEnumerable(keySelector, comparer, descending: false);
		}

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> ThenByDescending<TSource, TKey>(this IOrderedAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			return source.CreateOrderedEnumerable(keySelector, comparer, descending: true);
		}

		#endregion

		// If you are bored, maybe consider adding:
		// - DefaultIfEmpty<T>
		// - Zip<T>
		// - OrderBy<TElement> and OrderBy<TElement, TKey>
		// - GroupBy<TKey, TElement>

		#endregion

		#region Leaving the Monad...

		/// <summary>Execute an action for each element of an async sequence</summary>
		public static Task ForEachAsync<TElement>(this IAsyncQuery<TElement> source, [InstantHandle] Action<TElement> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync(action),
				_ => Run(source, AsyncIterationHint.All, action)
			};
		}

		/// <summary>Executes an action for each element of an async sequence</summary>
		public static Task ForEachAsync<TState, TElement>(this IAsyncQuery<TElement> source, TState state, [InstantHandle] Action<TState, TElement> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync(state, action),
				_ => Run(source, AsyncIterationHint.All, state, action)
			};
		}

		/// <summary>Executes an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<TElement>(this IAsyncQuery<TElement> source, [InstantHandle] Func<TElement, Task> asyncAction)
		{
			Contract.NotNull(asyncAction);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync(TaskHelpers.WithCancellation(asyncAction)),
				_ => ForEachAsync(source, TaskHelpers.WithCancellation(asyncAction))
			};
		}

		/// <summary>Executes an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<TElement>(this IAsyncQuery<TElement> source, [InstantHandle] Func<TElement, CancellationToken, Task> asyncAction)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncAction);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync(asyncAction),
				_ => Run(source, AsyncIterationHint.All, asyncAction)
			};
		}

		#region ToList/Array/Dictionary/HashSet...

		/// <summary>Creates a list from an async sequence.</summary>
		public static Task<List<T>> ToListAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToListAsync();
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				static (b, x) => b.Add(x),
				static (b) => b.ToListAndClear()
			);
		}

		/// <summary>Creates a list from an async sequence.</summary>
		public static Task<ImmutableArray<T>> ToImmutableArrayAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToImmutableArrayAsync();
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				static (b, x) => b.Add(x),
				static (b) => b.ToImmutableArrayAndClear()
			);
		}

		/// <summary>Creates an array from an async sequence.</summary>
		public static Task<T[]> ToArrayAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToArrayAsync();
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				static (b, x) => b.Add(x),
				static (b) => b.ToArrayAndClear()
			);
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function and key comparer.</summary>
		public static Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>(this IAsyncQuery<TSource> source, [InstantHandle] Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey: notnull
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			if (source is IAsyncLinqQuery<TSource> query)
			{
				return query.ToDictionaryAsync(keySelector, comparer);
			}

			return AggregateAsync(
				source,
				(
					Results: new Dictionary<TKey, TSource>(comparer),
					KeySelector: keySelector
				),
				static (s, x) => s.Results.Add(s.KeySelector(x), x),
				static (s) => s.Results
			);
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function, a comparer, and an element selector function.</summary>
		public static Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>(this IAsyncQuery<TSource> source, [InstantHandle] Func<TSource, TKey> keySelector, [InstantHandle] Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey: notnull
		{
			Contract.NotNull(keySelector);
			Contract.NotNull(elementSelector);

			if (source is IAsyncLinqQuery<TSource> query)
			{
				return query.ToDictionaryAsync(keySelector, elementSelector, comparer);
			}

			return AggregateAsync(
				source,
				(
					Results: new Dictionary<TKey, TElement>(comparer),
					KeySelector: keySelector,
					ElementSelector: elementSelector
				),
				static (s, x) => s.Results.Add(s.KeySelector(x), s.ElementSelector(x)),
				static (s) => s.Results
			);
		}

		/// <summary>Creates a Dictionary from an async sequence of pairs of keys and values.</summary>
		public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IAsyncQuery<KeyValuePair<TKey, TValue>> source, IEqualityComparer<TKey>? comparer = null)
			where TKey: notnull
		{
			return ToDictionaryAsync(source, kv => kv.Key, kv => kv.Value, comparer);
		}

		/// <summary>Creates a Hashset from an async sequence.</summary>
		public static Task<HashSet<T>> ToHashSetAsync<T>(this IAsyncQuery<T> source, IEqualityComparer<T>? comparer = null)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToHashSetAsync(comparer);
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				(buffer, x) => buffer.Add(x),
				(buffer) => buffer.ToHashSetAndClear(comparer)
			);
		}

		#endregion

		#region Aggregate...

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TSource> AggregateAsync<TSource>(this IAsyncQuery<TSource> source, [InstantHandle] Func<TSource, TSource, TSource> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			await using (var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All))
			{
				Contract.Debug.Assert(iterator != null, "The sequence returned a null async iterator");

				if (!(await iterator.MoveNextAsync().ConfigureAwait(false)))
				{
					throw ErrorNoElements();
				}

				var item = iterator.Current;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					item = aggregator(item, iterator.Current);
				}

				return item;
			}
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TAggregate> AggregateAsync<TSource, TAggregate>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Func<TAggregate, TSource, TAggregate> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			//TODO: optimize this to not have to allocate lambdas!
			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }).ConfigureAwait(false);
			return accumulate;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TAggregate> AggregateAsync<TSource, TAggregate>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Action<TAggregate, TSource> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			await ForEachAsync(
				source,
				(Fn: aggregator, Acc: seed),
				static (s, x) => s.Fn(s.Acc, x)
			).ConfigureAwait(false);

			return seed;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAggregate, TResult>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Func<TAggregate, TSource, TAggregate> aggregator, [InstantHandle] Func<TAggregate, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }).ConfigureAwait(false);
			return resultSelector(accumulate);
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAggregate, TResult>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Action<TAggregate, TSource> aggregator, [InstantHandle] Func<TAggregate, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			await ForEachAsync(
				source,
				(Fn: aggregator, Acc: seed),
				static (s, x) => s.Fn(s.Acc, x)
			).ConfigureAwait(false);

			return resultSelector(seed);
		}

		#endregion

		#region First...

		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> FirstAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.FirstAsync();
			}

			return Impl(source);

			static async Task<T> Impl(IAsyncQuery<T> source)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Head);

				if (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					return iterator.Current;
				}

				throw ErrorNoElements();
			}
		}

		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> FirstAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.FirstAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, bool> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

				while(await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (predicate(iterator.Current))
					{
						return iterator.Current;
					}
				}

				throw ErrorNoElements();
			}
		}

		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> FirstAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.FirstAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

				var ct = source.Cancellation;
				while(await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (await predicate(iterator.Current, ct).ConfigureAwait(false))
					{
						return iterator.Current;
					}
				}

				throw ErrorNoElements();
			}
		}

		#endregion

		#region FirstOrDefaultAsync...

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> FirstOrDefaultAsync<T>(this IAsyncQuery<T> source)
			=> FirstOrDefaultAsync(source, default(T?));

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IAsyncQuery<T> source, T defaultValue)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.FirstOrDefaultAsync(defaultValue);
			}

			return Impl(source, defaultValue);

			static async Task<T> Impl(IAsyncQuery<T> source, T defaultValue)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Head);

				if (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					return iterator.Current;
				}

				return defaultValue;
			}
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> FirstOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
			=> FirstOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate, T defaultValue)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.FirstOrDefaultAsync(predicate, defaultValue);
			}

			return Impl(source, predicate, defaultValue);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, bool> predicate, T defaultValue)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

				while(await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (predicate(iterator.Current))
					{
						return iterator.Current;
					}
				}

				return defaultValue;
			}
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> FirstOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
			=> FirstOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.FirstOrDefaultAsync(predicate, defaultValue);
			}

			return Impl(source, predicate, defaultValue);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

				var ct = source.Cancellation;
				while(await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (await predicate(iterator.Current,ct).ConfigureAwait(false))
					{
						return iterator.Current;
					}
				}

				return defaultValue;
			}
		}

		#endregion

		#region SingleAsync...

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleAsync();
			}

			return Head(source, single: true, orDefault: false, default!);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleAsync(predicate);
			}

			return Head(source.Where(predicate), single: true, orDefault: false, default!);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleAsync(predicate);
			}

			return Head(source.Where(predicate), single: true, orDefault: false, default!);
		}

		#endregion

		#region SingleOrDefaultAsync...

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T?> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source) => SingleOrDefaultAsync(source, default(T?));

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, T defaultValue)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleOrDefaultAsync(defaultValue);
			}

			return Head(source, single: true, orDefault: true, defaultValue);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T?> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate) => SingleOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate, T defaultValue)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleOrDefaultAsync(predicate, defaultValue);
			}

			return Head(source.Where(predicate), single: true, orDefault: true, defaultValue);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T?> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate) => SingleOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleOrDefaultAsync(predicate, defaultValue);
			}

			return Head(source.Where(predicate), single: true, orDefault: true, defaultValue);
		}

		#endregion

		#region LastAsync...

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> LastAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.LastAsync();
			}

			return Impl(source);

			static async Task<T> Impl(IAsyncQuery<T> source)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				bool found = false;
				T last = default!;

				while(await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					found = true;
					last = iterator.Current;
				}

				return found ? last : throw ErrorNoElements();
			}
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> LastAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.LastAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, bool> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				T result = default!;
				bool found = false;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var item = iterator.Current;
					if (predicate(item))
					{
						found = true;
						result = item;
					}
				}

				return found ? result : throw ErrorNoElements();
			}
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> LastAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.LastAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				T result = default!;
				bool found = false;
				var ct = source.Cancellation;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var item = iterator.Current;
					if (await predicate(item, ct).ConfigureAwait(false))
					{
						found = true;
						result = item;
					}
				}

				return found ? result : throw ErrorNoElements();
			}
		}

		#endregion

		#region LastOrDefaultAsync...

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> LastOrDefaultAsync<T>(this IAsyncQuery<T> source) => LastOrDefaultAsync(source, default(T?));

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, T defaultValue)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.LastOrDefaultAsync(defaultValue);
			}

			return Impl(source, defaultValue);

			static async Task<T> Impl(IAsyncQuery<T> source, T defaultValue)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					return defaultValue;
				}

				T result;
				do
				{
					result = iterator.Current;
				}
				while (await iterator.MoveNextAsync().ConfigureAwait(false));

				return result;
			}
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate) => LastOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate, T defaultValue)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.LastOrDefaultAsync(predicate, defaultValue);
			}

			return Impl(source, predicate, defaultValue);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, bool> predicate, T defaultValue)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				T result = defaultValue;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var item = iterator.Current;
					if (predicate(item))
					{
						result = iterator.Current;
					}
				}
				return result;
			}
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate) => LastOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.LastOrDefaultAsync(predicate, defaultValue);
			}

			return Impl(source, predicate, defaultValue);

			static async Task<T> Impl(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				T result = defaultValue;
				var ct = source.Cancellation;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var item = iterator.Current;
					if (await predicate(item, ct).ConfigureAwait(false))
					{
						result = iterator.Current;
					}
				}
				return result;
			}
		}

		#endregion

		#region ElemetAtAsync...

		/// <summary>Returns the element at a specific location of an async sequence, or an exception if there are not enough elements</summary>
		public static async Task<T> ElementAtAsync<T>(this IAsyncQuery<T> source, int index)
		{
			Contract.NotNull(source);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

			int counter = index;
			T item = default!;
			await Run(
				source,
				AsyncIterationHint.All,
				(x) =>
				{
					if (counter-- == 0) { item = x; return false; }
					return true;
				}
			).ConfigureAwait(false);

			if (counter >= 0) throw new InvalidOperationException("The sequence was too small");
			return item;
		}

		/// <summary>Returns the element at a specific location of an async sequence, or the default value for the type if there are not enough elements</summary>
		public static async Task<T> ElementAtOrDefaultAsync<T>(this IAsyncQuery<T> source, int index)
		{
			Contract.NotNull(source);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

			int counter = index;
			T item = default!;

			//TODO: use ExecuteAsync() if the source is an Iterator!
			await Run(
				source,
				AsyncIterationHint.All,
				(x) =>
				{
					if (counter-- == 0) { item = x; return false; }
					return true;
				}
			).ConfigureAwait(false);

			if (counter >= 0) return default!;
			return item;
		}

		#endregion

		#region CountAsync...

		/// <summary>Returns the number of elements in an async sequence.</summary>
		public static Task<int> CountAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.CountAsync();
			}

			return Impl(source);

			static async Task<int> Impl(IAsyncQuery<T> source)
			{
				int count = 0;
				await ForEachAsync(source, (_) => { ++count; }).ConfigureAwait(false);
				return count;
			}
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static Task<int> CountAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.CountAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<int> Impl(IAsyncQuery<T> source, Func<T, bool> predicate)
			{
				int count = 0;

				await ForEachAsync(source, (x) =>
				{
					if (predicate(x))
					{
						checked { ++count; }
					}
				}).ConfigureAwait(false);

				return count;
			}
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static Task<int> CountAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.CountAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<int> Impl(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
			{
				int count = 0;

				await ForEachAsync(source, async (x, ct) => {
					if (await predicate(x, ct).ConfigureAwait(false))
					{
						checked { ++count; }
					}
				}).ConfigureAwait(false);

				return count;
			}
		}

		#endregion

		#region SumAsync...

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<T> SumAsync<T>(this IAsyncQuery<T> source)
			where T : INumberBase<T>
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SumAsync();
			}

			if (typeof(T) == typeof(int)) return (Task<T>) (object) SumAsyncInt32Impl((IAsyncQuery<int>) source);
			if (typeof(T) == typeof(long)) return (Task<T>) (object) SumAsyncInt64Impl((IAsyncQuery<long>) source);
			if (typeof(T) == typeof(float)) return (Task<T>) (object) SumAsyncFloatImpl((IAsyncQuery<float>) source);
			if (typeof(T) == typeof(double)) return (Task<T>) (object) SumAsyncDoubleImpl((IAsyncQuery<double>) source);
			if (typeof(T) == typeof(decimal)) return (Task<T>) (object) SumAsyncDecimalImpl((IAsyncQuery<decimal>) source);

			return SumAsyncImpl(source);
		}

		internal static async Task<T> SumAsyncImpl<T>(IAsyncQuery<T> source) where T : INumberBase<T>
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			T sum = T.Zero;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum = checked(sum + iterator.Current);
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<T?> SumAsync<T>(this IAsyncQuery<T?> source)
			where T : struct, INumberBase<T>
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T?> query)
			{
				return query.SumAsync();
			}

			if (typeof(T) == typeof(int)) return (Task<T?>) (object) SumAsyncInt32Impl((IAsyncQuery<int?>) source);
			if (typeof(T) == typeof(long)) return (Task<T?>) (object) SumAsyncInt64Impl((IAsyncQuery<long?>) source);
			if (typeof(T) == typeof(float)) return (Task<T?>) (object) SumAsyncFloatImpl((IAsyncQuery<float?>) source);
			if (typeof(T) == typeof(double)) return (Task<T?>) (object) SumAsyncDoubleImpl((IAsyncQuery<double?>) source);
			if (typeof(T) == typeof(decimal)) return (Task<T?>) (object) SumAsyncDecimalImpl((IAsyncQuery<decimal?>) source);

			return SumAsyncNullableImpl(source);
		}

		internal static async Task<T?> SumAsyncNullableImpl<T>(IAsyncQuery<T?> source) where T : struct, INumberBase<T>
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			T sum = T.Zero;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<int> SumAsync(this IAsyncQuery<int> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<int> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt32Impl(source);
		}

		internal static async Task<int> SumAsyncInt32Impl(IAsyncQuery<int> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked { sum += iterator.Current; }
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<int?> SumAsync(this IAsyncQuery<int?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<int?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt32Impl(source);
		}

		internal static async Task<int?> SumAsyncInt32Impl(IAsyncQuery<int?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<long> SumAsync(this IAsyncQuery<long> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<long> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt64Impl(source);
		}

		internal static async Task<long> SumAsyncInt64Impl(IAsyncQuery<long> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			long sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked { sum += iterator.Current; }
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<long?> SumAsync(this IAsyncQuery<long?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<long?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt64Impl(source);
		}

		internal static async Task<long?> SumAsyncInt64Impl(IAsyncQuery<long?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			long sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<float> SumAsync(this IAsyncQuery<float> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<float> query)
			{
				return query.SumAsync();
			}

			return SumAsyncFloatImpl(source);
		}

		internal static async Task<float> SumAsyncFloatImpl(IAsyncQuery<float> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			//note: like Enumerable and AsyncEnumerable, we will also use a double as the accumulator (to reduce precision loss)

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return (float) sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<float?> SumAsync(this IAsyncQuery<float?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<float?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncFloatImpl(source);
		}

		internal static async Task<float?> SumAsyncFloatImpl(IAsyncQuery<float?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			//note: like Enumerable and AsyncEnumerable, we will also use a double as the accumulator (to reduce precision loss)
			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return (float) sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<double> SumAsync(this IAsyncQuery<double> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<double> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDoubleImpl(source);
		}

		internal static async Task<double> SumAsyncDoubleImpl(IAsyncQuery<double> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<double?> SumAsync(this IAsyncQuery<double?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<double?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDoubleImpl(source);
		}

		internal static async Task<double?> SumAsyncDoubleImpl(IAsyncQuery<double?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<decimal> SumAsync(this IAsyncQuery<decimal> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<decimal> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDecimalImpl(source);
		}

		internal static async Task<decimal> SumAsyncDecimalImpl(IAsyncQuery<decimal> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			decimal sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<decimal?> SumAsync(this IAsyncQuery<decimal?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<decimal?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDecimalImpl(source);
		}

		internal static async Task<decimal?> SumAsyncDecimalImpl(IAsyncQuery<decimal?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			decimal sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return sum;
		}

		#endregion

		#region AsEnumerable...

		/// <summary>Helper method that checks that the cancellation token is the same as the source</summary>
		/// <exception cref="InvalidOperationException">If the token is different</exception>
		/// <remarks>This is used to simplify the pattern of adapting <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> calls.</remarks>
		[MustDisposeResource]
		public static IAsyncEnumerator<T> GetCancellableAsyncEnumerator<T>(IAsyncQuery<T> query, AsyncIterationHint hint, CancellationToken ct)
		{
			if (ct.CanBeCanceled && !ct.Equals(query.Cancellation))
			{
				throw new InvalidOperationException("The CancellationToken that is passed to GetAsyncEnumerator() MUST be the same as the source!");
			}
			return query.GetAsyncEnumerator(hint);
		}

		/// <summary>Adapts this query into the equivalent <see cref="IAsyncEnumerable{T}"/></summary>
		/// <param name="source">Source query that will be adapted into an <see cref="IAsyncEnumerable{T}"/></param>
		/// <param name="hint">Hint passed to the source provider.</param>
		/// <returns>Sequence that will asynchronously return the results of this query.</returns>
		/// <remarks>
		/// <para>For best performance, the caller should take care to provide a <see cref="hint"/> that matches how this query will be consumed downstream.</para>
		/// <para>If the hint does not match, performance may be degraded.
		/// For example, if the caller will consumer this query using <c>await foreach</c> or <c>ToListAsync</c>, but uses <see cref="AsyncIterationHint.Iterator"/>, the provider may fetch small pages initially, before ramping up.
		/// The opposite is also true if the caller uses <see cref="AsyncIterationHint.All"/> but consumes the query using <c>AnyAsync()</c> or <c>FirstOrDefaultAsync</c>, the provider may fetch large pages and waste most of it except the first few elements.
		/// </para>
		/// </remarks>
		public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IAsyncQuery<T> source, AsyncIterationHint hint = AsyncIterationHint.Default)
			=> source is IAsyncLinqQuery<T> query ? query.ToAsyncEnumerable() : new ConfiguredAsyncEnumerable<T>(source, hint);

		public static IAsyncEnumerable<T> WantAll<T>(this IAsyncQuery<T> source)
			=> new ConfiguredAsyncEnumerable<T>(source, AsyncIterationHint.All);

		/// <summary>Exposes an async query as a regular <see cref="IAsyncEnumerable{T}"/>, with en explicit <see cref="AsyncIterationHint"/></summary>
		internal sealed class ConfiguredAsyncEnumerable<T> : IAsyncEnumerable<T>
		{

			private IAsyncQuery<T> Source { get; }

			private AsyncIterationHint Hint { get; }

			public ConfiguredAsyncEnumerable(IAsyncQuery<T> source, AsyncIterationHint hint)
			{
				this.Source = source;
				this.Hint = hint;
			}

			public CancellationToken Cancellation => this.Source.Cancellation;

			[MustDisposeResource]
			public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
			{
				//BUGBUG: if ct is not None and not the same as the source, we should maybe mix them!?
				return this.Source.GetAsyncEnumerator(this.Hint);
			}

		}

		public static IAsyncLinqQuery<T> ToAsyncQuery<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			if (source is IAsyncLinqQuery<T> query)
			{
				if (ct.CanBeCanceled && !ct.Equals(query.Cancellation))
				{
					throw new InvalidOperationException("The CancellationToken that is passed to ToAsyncQuery() MUST be the same as the source!");
				}
				return query;
			}
			return new AdapterAsyncEnumerable<T>(source, ct);
		}

		internal sealed class AdapterAsyncEnumerable<T> : AsyncLinqIterator<T>
		{

			private IAsyncEnumerable<T> Source { get; }

			private IAsyncEnumerator<T>? Iterator { get; set; }

			public AdapterAsyncEnumerable(IAsyncEnumerable<T> source, CancellationToken ct)
			{
				this.Source = source;
				this.Cancellation = ct;
			}

			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override AdapterAsyncEnumerable<T> Clone() => new(this.Source, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				this.Iterator = this.Source.GetAsyncEnumerator(this.Cancellation);
				return new(true);
			}

			/// <inheritdoc />
			protected override async ValueTask<bool> OnNextAsync()
			{
				var iterator = this.Iterator;
				if (iterator == null || !(await iterator.MoveNextAsync().ConfigureAwait(false)))
				{
					return await this.Completed().ConfigureAwait(false);
				}
				return Publish(iterator.Current);
			}

			/// <inheritdoc />
			protected override async ValueTask Cleanup()
			{
				var iterator = this.Iterator;
				this.Iterator = null;
				if (iterator != null)
				{
					await iterator.DisposeAsync().ConfigureAwait(false);
				}
			}

#if NET10_0_OR_GREATER

			/// <inheritdoc />
			public override Task<T[]> ToArrayAsync() => this.Source.ToArrayAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<List<T>> ToListAsync() => this.Source.ToListAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => this.Source.AnyAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<bool> AnyAsync(Func<T, bool> predicate) => this.Source.AnyAsync(predicate, this.Cancellation).AsTask();

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<T, TNew> selector) => new AdapterAsyncEnumerable<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<T, int, TNew> selector) => new AdapterAsyncEnumerable<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<T> Where(Func<T, bool> predicate) => new AdapterAsyncEnumerable<T>(this.Source.Where(predicate), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<T> Where(Func<T, int, bool> predicate) => new AdapterAsyncEnumerable<T>(this.Source.Where(predicate), this.Cancellation);

#endif

		}

		#endregion

		#region Min/Max...

		/// <summary>Returns the smallest value in the specified async sequence</summary>
		public static Task<T?> MinAsync<T>(this IAsyncQuery<T> source, IComparer<T>? comparer = null)
		{
			Contract.NotNull(source);
			comparer ??= Comparer<T>.Default;

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.MinAsync(comparer);
			}

			return MinAsyncImpl(source, comparer);

		}

		internal static async Task<T?> MinAsyncImpl<T>(IAsyncQuery<T> source, IComparer<T> comparer)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			if (default(T) is null)
			{
				// we will return null if the query is empty, or only contains null
				var min = default(T);
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (candidate is not null && (min is null || comparer.Compare(candidate, candidate) > 0))
					{
						min = candidate;
					}
				}
				return min;
			}
			else
			{
				// we will throw if the query is empty

				// get the first
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					throw ErrorNoElements();
				}
				var min = iterator.Current;

				// compare with the rest
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (comparer.Compare(candidate, min) < 0)
					{
						min = candidate;
					}
				}

				return min;
			}
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static Task<T?> MaxAsync<T>(this IAsyncQuery<T> source, IComparer<T>? comparer = null)
		{
			Contract.NotNull(source);
			comparer ??= Comparer<T>.Default;

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.MaxAsync(comparer);
			}

			return MaxAsyncImpl(source, comparer);
		}

		internal static async Task<T?> MaxAsyncImpl<T>(IAsyncQuery<T> source, IComparer<T> comparer)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			if (default(T) is null)
			{
				var max = default(T);
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (candidate is not null && (max is null || comparer.Compare(candidate, candidate) > 0))
					{
						max = candidate;
					}
				}
				return max;
			}
			else
			{
				// get the first
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					throw ErrorNoElements();
				}
				var max = iterator.Current;

				// compare with the rest
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (comparer.Compare(candidate, max) > 0)
					{
						max = candidate;
					}
				}

				return max;
			}
		}

		#endregion

		#region Any/None...

		/// <summary>Determines whether an async sequence contains any elements.</summary>
		/// <remarks>This is the logical equivalent to "source.Count() > 0" but can be better optimized by some providers</remarks>
		public static Task<bool> AnyAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.AnyAsync();
			}

			return Impl(source);

			static async Task<bool> Impl(IAsyncQuery<T> source)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Head);
				return await iterator.MoveNextAsync().ConfigureAwait(false);
			}
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static Task<bool> AnyAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.AnyAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<bool> Impl(IAsyncQuery<T> source, Func<T, bool> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (predicate(iterator.Current)) return true;
				}

				return false;
			}
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static Task<bool> AnyAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.AnyAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<bool> Impl(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

				var ct = source.Cancellation;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (await predicate(iterator.Current, ct).ConfigureAwait(false)) return true;
				}

				return false;
			}
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static Task<bool> AllAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.AllAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<bool> Impl(IAsyncQuery<T> source, Func<T, bool> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (!predicate(iterator.Current)) return false;
				}

				return true;
			}
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static Task<bool> AllAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.AllAsync(predicate);
			}

			return Impl(source, predicate);

			static async Task<bool> Impl(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				var ct = source.Cancellation;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (!(await predicate(iterator.Current, ct).ConfigureAwait(false))) return false;
				}

				return true;
			}
		}

		#endregion

		#endregion

		#region Query Statistics...

		//TODO: move this somewhere else?

		/// <summary>Measure the number of items that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="counter"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<TSource> WithCountStatistics<TSource>(this IAsyncQuery<TSource> source, out QueryStatistics<int> counter)
		{
			Contract.NotNull(source);

			var signal = new QueryStatistics<int>(0);
			counter = signal;

			// to count, we just increment the signal each type a value flows through here
			return Select(source, (x) =>
			{
				signal.Update(checked(signal.Value + 1));
				return x;
			});
		}

		/// <summary>Measure the number and size of slices that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<KeyValuePair<Slice, Slice>> WithSizeStatistics(this IAsyncQuery<KeyValuePair<Slice, Slice>> source, out QueryStatistics<KeyValueSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new KeyValueSizeStatistics();
			statistics = new(data);

			// to count, we just increment the signal each type a value flows through here
			return Select(source,(kvp) =>
			{
				data.Add(kvp.Key.Count, kvp.Value.Count);
				return kvp;
			});
		}

		/// <summary>Measure the number and sizes of the keys and values that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<Slice> WithSizeStatistics(this IAsyncQuery<Slice> source, out QueryStatistics<DataSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new DataSizeStatistics();
			statistics = new(data);

			// to count, we just increment the signal each type a value flows through here
			return Select(source, (x) =>
			{
				data.Add(x.Count);
				return x;
			});
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="handler"/> is execute inline before passing the item down the line, and should not block</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<TSource> Observe<TSource>(this IAsyncQuery<TSource> source, Action<TSource> handler)
		{
			Contract.NotNull(source);
			Contract.NotNull(handler);

			return new ObserverAsyncIterator<TSource>(source, new(handler));
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="asyncHandler"/> is execute inline before passing the item down the line, and should not block</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<TSource> Observe<TSource>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task> asyncHandler)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncHandler);

			return new ObserverAsyncIterator<TSource>(source, new(asyncHandler));
		}

		#endregion

		#region Error Helpers...


		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorNoElements() => new("Sequence contains no elements");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorMoreThenOneElement() => new("Sequence contains more than one elements");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorNoMatch() => new("Sequence contains no matching element");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorMoreThanOneMatch() => new("Sequence contains more than one matching element");

		#endregion

	}

}
