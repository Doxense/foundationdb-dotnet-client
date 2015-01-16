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

namespace FoundationDB.Linq
{
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Provides a set of static methods for querying objects that implement <see cref="IFdbAsyncEnumerable{T}"/>.</summary>
	public static partial class FdbAsyncEnumerable
	{
		// Welcome to the wonderful world of the Monads!

		#region Entering the Monad...

		/// <summary>Returns an empty async sequence</summary>
		[NotNull]
		public static IFdbAsyncEnumerable<T> Empty<T>()
		{
			return EmptySequence<T>.Default;
		}

		/// <summary>Returns an async sequence with a single element, which is a constant</summary>
		[NotNull]
		public static IFdbAsyncEnumerable<T> Singleton<T>(T value)
		{
			//note: we can't call this method Single<T>(T), because then Single<T>(Func<T>) would be ambigous with Single<Func<T>>(T)
			return new SingletonSequence<T>(() => value);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="lambda">Lambda that will be called once per iteration, to produce the single element of this sequene</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="lambda"/> will be called once for each iteration.</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<T> Single<T>([NotNull] Func<T> lambda)
		{
			if (lambda == null) throw new ArgumentNullException("lambda");
			return new SingletonSequence<T>(lambda);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="asyncLambda">Lambda that will be called once per iteration, to produce the single element of this sequene</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="asyncLambda"/> will be called once for each iteration.</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<T> Single<T>([NotNull] Func<Task<T>> asyncLambda)
		{
			if (asyncLambda == null) throw new ArgumentNullException("asyncLambda");
			return new SingletonSequence<T>(asyncLambda);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="asyncLambda">Lambda that will be called once per iteration, to produce the single element of this sequene</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="asyncLambda"/> will be called once for each iteration.</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<T> Single<T>([NotNull] Func<CancellationToken, Task<T>> asyncLambda)
		{
			if (asyncLambda == null) throw new ArgumentNullException("asyncLambda");
			return new SingletonSequence<T>(asyncLambda);
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<R> ToAsyncEnumerable<T, R>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, Task<R>> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return Create<T, R>(source, (iterator) => new EnumerableIterator<T, R>(iterator, lambda));
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<T> ToAsyncEnumerable<T>([NotNull] this IEnumerable<T> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			return Create<T, T>(source, (iterator) => new EnumerableIterator<T, T>(iterator, x => Task.FromResult(x)));
		}

		/// <summary>Wraps an async lambda into an async sequence that will return the result of the lambda</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<T> FromTask<T>([NotNull] Func<Task<T>> asyncLambda)
		{
			//TODO: create a custom iterator for this ?
			return ToAsyncEnumerable(new [] { asyncLambda }).Select(x => x());
		}

		/// <summary>Split a sequence of items into several batches</summary>
		/// <typeparam name="T">Type of the elemenst in <paramref name="source"/></typeparam>
		/// <param name="source">Source sequence</param>
		/// <param name="batchSize">Maximum size of each batch</param>
		/// <returns>Sequence of batches, whose size will always we <paramref name="batchSize"/>, except for the last batch that will only hold the remaning items. If the source is empty, an empty sequence is returned.</returns>
		[NotNull, LinqTunnel]
		public static IEnumerable<List<T>> Buffered<T>([NotNull] this IEnumerable<T> source, int batchSize)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (batchSize <= 0) throw new ArgumentException("Batch size must be greater than zero.", "batchSize");

			var list = new List<T>(batchSize);
			foreach (var item in source)
			{
				list.Add(item);
				if (list.Count >= batchSize)
				{
					yield return list;
					list.Clear();
				}
			}
		}

		#endregion

		#region Staying in the Monad...

		#region SelectMany...

		/// <summary>Projects each element of an async sequence to an <see cref="IFdbAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, IEnumerable<TResult>> selector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.SelectMany<TResult>(selector);
			}

			return Flatten<TSource, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TResult>>(selector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IFdbAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, Task<IEnumerable<TResult>>> asyncSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return SelectMany<TSource, TResult>(source, TaskHelpers.WithCancellation(asyncSelector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IFdbAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.SelectMany<TResult>(asyncSelector);
			}

			return Flatten<TSource, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TResult>>(asyncSelector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IFdbAsyncEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, IEnumerable<TCollection>> collectionSelector, [NotNull] Func<TSource, TCollection, TResult> resultSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (collectionSelector == null) throw new ArgumentNullException("collectionSelector");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.SelectMany<TCollection, TResult>(collectionSelector, resultSelector);
			}

			return Flatten<TSource, TCollection, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TCollection>>(collectionSelector), resultSelector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IFdbAsyncEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, Task<IEnumerable<TCollection>>> asyncCollectionSelector, [NotNull] Func<TSource, TCollection, TResult> resultSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncCollectionSelector == null) throw new ArgumentNullException("asyncCollectionSelector");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			return SelectMany<TSource, TCollection, TResult>(source, TaskHelpers.WithCancellation(asyncCollectionSelector), resultSelector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IFdbAsyncEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, [NotNull] Func<TSource, TCollection, TResult> resultSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncCollectionSelector == null) throw new ArgumentNullException("asyncCollectionSelector");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.SelectMany<TCollection, TResult>(asyncCollectionSelector, resultSelector);
			}

			return Flatten<TSource, TCollection, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TCollection>>(asyncCollectionSelector), resultSelector);
		}

		#endregion

		#region Select...

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, TResult> selector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.Select<TResult>(selector);
			}

			return Map<TSource, TResult>(source, new AsyncTransformExpression<TSource,TResult>(selector));
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, Task<TResult>> asyncSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return Select<TSource, TResult>(source, TaskHelpers.WithCancellation(asyncSelector));
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, CancellationToken, Task<TResult>> asyncSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.Select<TResult>(asyncSelector);
			}

			return Map<TSource, TResult>(source, new AsyncTransformExpression<TSource,TResult>(asyncSelector));
		}

		#endregion

		#region Where...

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> Where<TResult>([NotNull] this IFdbAsyncEnumerable<TResult> source, [NotNull] Func<TResult, bool> predicate)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (predicate == null) throw new ArgumentNullException("predicate");

			var iterator = source as FdbAsyncIterator<TResult>;
			if (iterator != null)
			{
				return iterator.Where(predicate);
			}

			return Filter<TResult>(source, new AsyncFilterExpression<TResult>(predicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<T> Where<T>([NotNull] this IFdbAsyncEnumerable<T> source, [NotNull] Func<T, Task<bool>> asyncPredicate)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncPredicate == null) throw new ArgumentNullException("asyncPredicate");

			return Where<T>(source, TaskHelpers.WithCancellation(asyncPredicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> Where<TResult>([NotNull] this IFdbAsyncEnumerable<TResult> source, [NotNull] Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncPredicate == null) throw new ArgumentNullException("asyncPredicate");

			var iterator = source as FdbAsyncIterator<TResult>;
			if (iterator != null)
			{
				return iterator.Where(asyncPredicate);
			}

			return Filter<TResult>(source, new AsyncFilterExpression<TResult>(asyncPredicate));
		}

		#endregion

		#region Take...

		/// <summary>Returns a specified number of contiguous elements from the start of an async sequence.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> Take<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, int count)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count cannot be less than zero");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.Take(count);
			}

			return FdbAsyncEnumerable.Limit<TSource>(source, count);
		}

		#endregion

		#region TakeWhile...

		/// <summary>Returns elements from an async sequence as long as a specified condition is true, and then skips the remaining elements.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> TakeWhile<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, bool> condition)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (condition == null) throw new ArgumentNullException("condition");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.TakeWhile(condition);
			}

			return FdbAsyncEnumerable.Limit<TSource>(source, condition);
		}

		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> TakeWhile<TSource>(this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, bool> condition, out QueryStatistics<bool> stopped)
		{
			var signal = new QueryStatistics<bool>(false);
			stopped = signal;

			// to trigger the signal, we just intercept the condition returning false (which only happen once!)
			Func<TSource, bool> wrapped = (x) =>
			{
				if (condition(x)) return true;
				signal.Update(true);
				return false;
			};

			return TakeWhile(source, wrapped);
		}

		#endregion

		#region Skip...

		/// <summary>Skips the first elements of an async sequence.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> Skip<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, int count)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count cannot be less than zero");

			var iterator = source as FdbAsyncIterator<TSource>;
			if (iterator != null)
			{
				return iterator.Skip(count);
			}

			return FdbAsyncEnumerable.Offset<TSource>(source, count);
		}

		#endregion

		#region SelectAsync

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TResult> SelectAsync<TSource, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, CancellationToken, Task<TResult>> asyncSelector, FdbParallelQueryOptions options = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return new FdbParallelSelectAsyncIterator<TSource, TResult>(source, asyncSelector, options ?? new FdbParallelQueryOptions());
		}

		/// <summary>Always prefetch the next item from the inner sequence.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <returns>Sequence that prefetch the next item, when outputing the current item.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid prefetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> Prefetch<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			return new FdbPrefetchingAsyncIterator<TSource>(source, 1);
		}

		/// <summary>Prefetch a certain number of items from the inner sequence, before outputing the results one by one.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <param name="prefetchCount">Maximum number of items to buffer from the source before they are consumed by the rest of the query.</param>
		/// <returns>Sequence that returns items from a buffer of prefetched list.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid prefetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> Prefetch<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, int prefetchCount)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (prefetchCount <= 0) throw new ArgumentOutOfRangeException("prefetchCount", prefetchCount, "Prefetch count must be at least one.");

			return new FdbPrefetchingAsyncIterator<TSource>(source, prefetchCount);
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
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource[]> Window<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, int maxWindowSize)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (maxWindowSize <= 0) throw new ArgumentOutOfRangeException("maxWindowSize", maxWindowSize, "Window size must be at least one.");

			return new FdbWindowingAsyncIterator<TSource>(source, maxWindowSize);
		}

		/// <summary>Buffers the items of a source sequence, and outputs a sequence of fixed-sized arrays.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that will be cut into chunks containing at most <paramref name="batchSize"/> items.</param>
		/// <param name="batchSize">Number of items per batch. The last batch may contain less items, but should never be empty.</param>
		/// <returns>Sequence of arrays of size <paramref name="batchSize"/>, except the last batch which can have less items.</returns>
		/// <remarks>
		/// This operator does not care about the latency of each item, and will always try to fill each batch completely, before outputing a result.
		/// If you are working on an inner sequence that is bursty in nature, where items arrives in waves, you should use <see cref="Window{TSource}"/> which attempts to minimize the latency by outputing incomplete batches if needed.
		/// </remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource[]> Batch<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, int batchSize)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (batchSize <= 0) throw new ArgumentOutOfRangeException("batchSize", batchSize, "Batch size must be at least one.");

			return new FdbBatchingAsyncIterator<TSource>(source, batchSize);
		}

		#endregion

		#region Distinct...

		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> Distinct<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, IEqualityComparer<TSource> comparer = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			comparer = comparer ?? EqualityComparer<TSource>.Default;

			return new FdbDistinctAsyncIterator<TSource>(source, comparer);
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
		public static Task ForEachAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, [NotNull, InstantHandle] Action<T> action, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			var iterator = source as FdbAsyncIterator<T>;
			if (iterator != null)
			{
				return iterator.ExecuteAsync(action, ct);
			}
			else
			{
				return Run<T>(source, FdbAsyncMode.All, action, ct);
			}
		}

		/// <summary>Execute an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, [NotNull, InstantHandle] Func<T, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");

			var iterator = source as FdbAsyncIterator<T>;
			if (iterator != null)
			{
				return iterator.ExecuteAsync(TaskHelpers.WithCancellation(asyncAction), ct);
			}
			else
			{
				return ForEachAsync<T>(source, TaskHelpers.WithCancellation(asyncAction), ct);
			}
		}

		/// <summary>Execute an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, [NotNull, InstantHandle] Func<T, CancellationToken, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");

			var iterator = source as FdbAsyncIterator<T>;
			if (iterator != null)
			{
				return iterator.ExecuteAsync(asyncAction, ct);
			}
			else
			{
				return Run<T>(source, FdbAsyncMode.All, asyncAction, ct);
			}
		}

		/// <summary>Create a list from an async sequence.</summary>
		public static Task<List<T>> ToListAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");

			return AggregateAsync(
				source,
				new Buffer<T>(),
				(buffer, x) => buffer.Add(x),
				(buffer) => buffer.ToList(),
				ct
			);
		}

		/// <summary>Create an array from an async sequence.</summary>
		public static Task<T[]> ToArrayAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");

			return AggregateAsync(
				source,
				new Buffer<T>(),
				(buffer, x) => buffer.Add(x),
				(buffer) => buffer.ToArray(),
				cancellationToken
			);
		}

		/// <summary>Create an array from an async sequence, knowing a rough estimation of the number of elements.</summary>
		internal static Task<T[]> ToArrayAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, int estimatedSize, CancellationToken cancellationToken = default(CancellationToken))
		{
			Contract.Requires(source != null && estimatedSize >= 0);

			return AggregateAsync(
				source,
				new List<T>(estimatedSize),
				(buffer, x) => buffer.Add(x),
				(buffer) => buffer.ToArray(),
				cancellationToken
			);
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function and key comparer.</summary>
		public static Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull, InstantHandle] Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (keySelector == null) throw new ArgumentNullException("keySelector");

			return AggregateAsync(
				source,
				new Dictionary<TKey, TSource>(comparer ?? EqualityComparer<TKey>.Default),
				(results, x) => { results[keySelector(x)] = x; },
				cancellationToken
			);
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function, a comparer, and an element selector function.</summary>
		public static Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull, InstantHandle] Func<TSource, TKey> keySelector, [NotNull, InstantHandle] Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (keySelector == null) throw new ArgumentNullException("keySelector");
			if (elementSelector == null) throw new ArgumentNullException("elementSelector");

			return AggregateAsync(
				source,
				new Dictionary<TKey, TElement>(comparer ?? EqualityComparer<TKey>.Default),
				(results, x) => { results[keySelector(x)] = elementSelector(x); },
				cancellationToken
			);
		}

		/// <summary>Creates a Dictionary from an async sequence of pairs of keys and values.</summary>
		public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>([NotNull] this IFdbAsyncEnumerable<KeyValuePair<TKey, TValue>> source, IEqualityComparer<TKey> comparer = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			cancellationToken.ThrowIfCancellationRequested();

			return AggregateAsync(
				source,
				new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default),
				(results, x) => { results[x.Key] = x.Value; },
				cancellationToken
			);
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TSource> AggregateAsync<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull, InstantHandle] Func<TSource, TSource, TSource> aggregator, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (aggregator == null) throw new ArgumentNullException("aggregator");

			cancellationToken.ThrowIfCancellationRequested();
			using (var iterator = source.GetEnumerator(FdbAsyncMode.All))
			{
				Contract.Assert(iterator != null, "The sequence returned a null async iterator");

				if (!(await iterator.MoveNext(cancellationToken).ConfigureAwait(false)))
				{
					throw new InvalidOperationException("The sequence was empty");
				}

				var item = iterator.Current;
				while (await iterator.MoveNext(cancellationToken).ConfigureAwait(false))
				{
					item = aggregator(item, iterator.Current);
				}

				return item;
			}
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TAccumulate> AggregateAsync<TSource, TAccumulate>([NotNull] this IFdbAsyncEnumerable<TSource> source, TAccumulate seed, [NotNull, InstantHandle] Func<TAccumulate, TSource, TAccumulate> aggregator, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (aggregator == null) throw new ArgumentNullException("aggregator");

			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }, cancellationToken).ConfigureAwait(false);
			return accumulate;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TAccumulate> AggregateAsync<TSource, TAccumulate>([NotNull] this IFdbAsyncEnumerable<TSource> source, TAccumulate seed, [NotNull, InstantHandle] Action<TAccumulate, TSource> aggregator, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (aggregator == null) throw new ArgumentNullException("aggregator");

			var accumulate = seed;
			await ForEachAsync(source, (x) => { aggregator(accumulate, x); }, cancellationToken).ConfigureAwait(false);
			return accumulate;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAccumulate, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, TAccumulate seed, [NotNull, InstantHandle] Func<TAccumulate, TSource, TAccumulate> aggregator, [NotNull, InstantHandle] Func<TAccumulate, TResult> resultSelector, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (aggregator == null) throw new ArgumentNullException("aggregator");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }, cancellationToken).ConfigureAwait(false);
			return resultSelector(accumulate);
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAccumulate, TResult>([NotNull] this IFdbAsyncEnumerable<TSource> source, TAccumulate seed, [NotNull, InstantHandle] Action<TAccumulate, TSource> aggregator, [NotNull, InstantHandle] Func<TAccumulate, TResult> resultSelector, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (aggregator == null) throw new ArgumentNullException("aggregator");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			var accumulate = seed;
			await ForEachAsync(source, (x) => aggregator(accumulate, x), cancellationToken);
			return resultSelector(accumulate);
		}

		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> FirstAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return rq.FirstAsync();

			return Head<T>(source, single: false, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefaultAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return rq.FirstOrDefaultAsync();

			return Head<T>(source, single: false, orDefault: true, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return rq.SingleAsync();

			return Head<T>(source, single: true, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return rq.SingleOrDefaultAsync();

			return Head<T>(source, single: true, orDefault: true, ct: ct);
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static async Task<T> LastAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return await rq.LastAsync();

			bool found = false;
			T last = default(T);

			await ForEachAsync<T>(source, (x) => { found = true; last = x; }, ct).ConfigureAwait(false);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return last;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefaultAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return await rq.LastOrDefaultAsync();

			bool found = false;
			T last = default(T);

			await ForEachAsync<T>(source, (x) => { found = true; last = x; }, ct).ConfigureAwait(false);

			return found ? last : default(T);
		}

		/// <summary>Returns the element at a specific location of an async sequence, or an exception if there are not enough elements</summary>
		public static async Task<T> ElementAtAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, int index, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (index < 0) throw new ArgumentOutOfRangeException("index");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return await rq.Skip(index).SingleAsync();

			int counter = index;
			T item = default(T);
			await Run<T>(
				source,
				FdbAsyncMode.All,
				(x) =>
				{
					if (counter-- == 0) { item = x; return false; }
					return true;
				},
				ct
			).ConfigureAwait(false);

			if (counter >= 0) throw new InvalidOperationException("The sequence was too small");
			return item;
		}

		/// <summary>Returns the element at a specific location of an async sequence, or the default value for the type if it there are not enough elements</summary>
		public static async Task<T> ElementAtOrDefaultAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, int index, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (index < 0) throw new ArgumentOutOfRangeException("index");
			ct.ThrowIfCancellationRequested();

			var rq = source as FdbRangeQuery<T>;
			if (rq != null) return await rq.Skip(index).SingleAsync();

			int counter = index;
			T item = default(T);

			//TODO: use ExecuteAsync() if the source is an Iterator!
			await Run<T>(
				source,
				FdbAsyncMode.All,
				(x) =>
				{
					if (counter-- == 0) { item = x; return false; }
					return true;
				},
				ct
			).ConfigureAwait(false);

			if (counter >= 0) return default(T);
			return item;
		}

		/// <summary>Returns the number of elements in an async sequence.</summary>
		public static async Task<int> CountAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			int count = 0;

			await ForEachAsync<T>(source, (_) => { ++count; }, ct).ConfigureAwait(false);

			return count;
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static async Task<int> CountAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, [NotNull, InstantHandle] Func<T, bool> predicate, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (predicate == null) throw new ArgumentNullException("predicate");

			int count = 0;

			await ForEachAsync<T>(source, (x) => { if (predicate(x)) ++count; }, ct).ConfigureAwait(false);

			return count;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static async Task<ulong> SumAsync([NotNull] this IFdbAsyncEnumerable<ulong> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");

			ulong sum = 0;

			await ForEachAsync<ulong>(source, (x) => { sum += x; }, ct).ConfigureAwait(false);

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static async Task<ulong> SumAsync([NotNull] this IFdbAsyncEnumerable<ulong> source, [NotNull, InstantHandle] Func<ulong, bool> predicate, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (predicate == null) throw new ArgumentNullException("predicate");

			ulong sum = 0;

			await ForEachAsync<ulong>(source, (x) => { if (predicate(x)) sum += x; }, ct).ConfigureAwait(false);

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static async Task<long> SumAsync([NotNull] this IFdbAsyncEnumerable<long> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");

			long sum = 0;

			await ForEachAsync<long>(source, (x) => { sum += x; }, ct).ConfigureAwait(false);

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static async Task<long> SumAsync([NotNull] this IFdbAsyncEnumerable<long> source, [NotNull, InstantHandle] Func<long, bool> predicate, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (predicate == null) throw new ArgumentNullException("predicate");

			long sum = 0;

			await ForEachAsync<long>(source, (x) => { if (predicate(x)) sum += x; }, ct).ConfigureAwait(false);

			return sum;
		}

		/// <summary>Returns the smallest value in the specified async sequence</summary>
		public static async Task<T> MinAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, IComparer<T> comparer = null, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			comparer = comparer ?? Comparer<T>.Default;

			bool found = false;
			T min = default(T);

			await ForEachAsync<T>(
				source,
				(x) =>
				{
					if (!found || comparer.Compare(x, min) < 0)
					{
						min = x;
						found = true;
					}
				},
				ct
			).ConfigureAwait(false);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return min;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<T> MaxAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, IComparer<T> comparer = null, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			comparer = comparer ?? Comparer<T>.Default;

			bool found = false;
			T max = default(T);

			await ForEachAsync<T>(
				source,
				(x) =>
				{
					if (!found || comparer.Compare(x, max) > 0)
					{
						max = x;
						found = true;
					}
				},
				ct
			).ConfigureAwait(false);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return max;
		}

		/// <summary>Determines whether an async sequence contains any elements.</summary>
		/// <remarks>This is the logical equivalent to "source.Count() > 0" but can be better optimized by some providers</remarks>
		public static async Task<bool> AnyAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			using (var iterator = source.GetEnumerator(FdbAsyncMode.Head))
			{
				return await iterator.MoveNext(ct).ConfigureAwait(false);
			}
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static async Task<bool> AnyAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, [NotNull, InstantHandle] Func<T, bool> predicate, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (predicate == null) throw new ArgumentNullException("predicate");
			ct.ThrowIfCancellationRequested();

			using (var iterator = source.GetEnumerator(FdbAsyncMode.Head))
			{
				while (await iterator.MoveNext(ct).ConfigureAwait(false))
				{
					if (predicate(iterator.Current)) return true;
				}
			}
			return false;
		}

		/// <summary>Determines wether an async sequence contains no elements at all.</summary>
		/// <remarks>This is the logical equivalent to "source.Count() == 0" or "!source.Any()" but can be better optimized by some providers</remarks>
		public static async Task<bool> NoneAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			ct.ThrowIfCancellationRequested();

			using (var iterator = source.GetEnumerator(FdbAsyncMode.Head))
			{
				return !(await iterator.MoveNext(ct).ConfigureAwait(false));
			}
		}

		/// <summary>Determines whether none of the elements of an async sequence satisfies a condition.</summary>
		public static async Task<bool> NoneAsync<T>([NotNull] this IFdbAsyncEnumerable<T> source, [NotNull, InstantHandle] Func<T, bool> predicate, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (predicate == null) throw new ArgumentNullException("predicate");
			ct.ThrowIfCancellationRequested();

			using (var iterator = source.GetEnumerator(FdbAsyncMode.Head))
			{
				while (await iterator.MoveNext(ct).ConfigureAwait(false))
				{
					if (predicate(iterator.Current)) return false;
				}
			}
			return true;
		}

		#endregion

		#region Query Statistics...

		//TODO: move this somewhere else?

		public class QueryStatistics<TData>
		{
			public QueryStatistics()
			{ }

			public QueryStatistics(TData value)
			{
				this.Value = value;
			}

			public TData Value { get; protected set; }

			public void Update(TData newValue)
			{
				this.Value = newValue;
			}
		}

		public class KeyValueSize
		{
			/// <summary>Total number of pairs of keys and values that have flowed through this point</summary>
			public long Count { get; private set; }

			/// <summary>Total size of all keys and values combined</summary>
			public long Size { get { return checked(this.KeySize + this.ValueSize); } }

			/// <summary>Total size of all keys combined</summary>
			public long KeySize { get; private set; }

			/// <summary>Total size of all values combined</summary>
			public long ValueSize { get; private set; }

			public void Add(int keySize, int valueSize)
			{
				this.Count++;
				this.KeySize = checked(keySize + this.KeySize);
				this.ValueSize = checked(valueSize + this.ValueSize);
			}
		}

		public class DataSize
		{
			/// <summary>Total number of items that have flowed through this point</summary>
			public long Count { get; private set; }

			/// <summary>Total size of all items that have flowed through this point</summary>
			public long Size { get; private set; }

			public void Add(int size)
			{
				this.Count++;
				this.Size = checked(size + this.Size);
			}
		}

		/// <summary>Measure the number of items that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="counter"/> are only safe to read once the query has ended</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> WithCountStatistics<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, out QueryStatistics<int> counter)
		{
			if (source == null) throw new ArgumentNullException("source");

			var signal = new QueryStatistics<int>(0);
			counter = signal;

			// to count, we just increment the signal each type a value flows through here
			Func<TSource, TSource> wrapped = (x) =>
			{
				signal.Update(checked(signal.Value + 1));
				return x;
			};

			return Select(source, wrapped);
		}

		/// <summary>Measure the number and size of slices that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>> WithSizeStatistics([NotNull] this IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>> source, out QueryStatistics<KeyValueSize> statistics)
		{
			if (source == null) throw new ArgumentNullException("source");

			var data = new KeyValueSize();
			statistics = new QueryStatistics<KeyValueSize>(data);

			// to count, we just increment the signal each type a value flows through here
			Func<KeyValuePair<Slice, Slice>, KeyValuePair<Slice, Slice>> wrapped = (kvp) =>
			{
				data.Add(kvp.Key.Count, kvp.Value.Count);
				return kvp;
			};

			return Select(source, wrapped);
		}

		/// <summary>Measure the number and sizes of the keys and values that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<Slice> WithSizeStatistics([NotNull] this IFdbAsyncEnumerable<Slice> source, out QueryStatistics<DataSize> statistics)
		{
			if (source == null) throw new ArgumentNullException("source");

			var data = new DataSize();
			statistics = new QueryStatistics<DataSize>(data);

			// to count, we just increment the signal each type a value flows through here
			Func<Slice, Slice> wrapped = (x) =>
			{
				data.Add(x.Count);
				return x;
			};

			return Select(source, wrapped);
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="handler"/> is execute inline before passing the item down the line, and should not block</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> Observe<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Action<TSource> handler)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (handler == null) throw new ArgumentNullException("handler");

			return new FdbObserverIterator<TSource>(source, new AsyncObserverExpression<TSource>(handler));
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="asyncHandler"/> is execute inline before passing the item down the line, and should not block</remarks>
		[NotNull, LinqTunnel]
		public static IFdbAsyncEnumerable<TSource> Observe<TSource>([NotNull] this IFdbAsyncEnumerable<TSource> source, [NotNull] Func<TSource, CancellationToken, Task> asyncHandler)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");

			return new FdbObserverIterator<TSource>(source, new AsyncObserverExpression<TSource>(asyncHandler));
		}

		#endregion

	}

}
