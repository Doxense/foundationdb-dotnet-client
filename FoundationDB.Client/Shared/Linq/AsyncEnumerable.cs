#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Linq
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq.Async.Expressions;
	using Doxense.Linq.Async.Iterators;
	using Doxense.Threading.Tasks;
	using JetBrains.Annotations;

	/// <summary>Provides a set of static methods for querying objects that implement <see cref="IAsyncEnumerable{T}"/>.</summary>
	[PublicAPI]
	public static partial class AsyncEnumerable
	{
		// Welcome to the wonderful world of the Monads!

		#region Entering the Monad...

		/// <summary>Returns an empty async sequence</summary>
		[Pure]
		public static IAsyncEnumerable<T> Empty<T>()
		{
			return EmptySequence<T>.Default;
		}

		/// <summary>Returns an async sequence with a single element, which is a constant</summary>
		[Pure]
		public static IAsyncEnumerable<T> Singleton<T>(T value)
		{
			//note: we can't call this method Single<T>(T), because then Single<T>(Func<T>) would be ambiguous with Single<Func<T>>(T)
			return new SingletonSequence<T>(() => value);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="lambda">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="lambda"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<T> Single<T>(Func<T> lambda)
		{
			Contract.NotNull(lambda);
			return new SingletonSequence<T>(lambda);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="asyncLambda">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="asyncLambda"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<T> Single<T>(Func<Task<T>> asyncLambda)
		{
			Contract.NotNull(asyncLambda);
			return new SingletonSequence<T>(asyncLambda);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="asyncLambda">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="asyncLambda"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<T> Single<T>(Func<CancellationToken, Task<T>> asyncLambda)
		{
			Contract.NotNull(asyncLambda);
			return new SingletonSequence<T>(asyncLambda);
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TOutput> ToAsyncEnumerable<TInput, TOutput>(this IEnumerable<TInput> source, Func<TInput, Task<TOutput>> lambda)
		{
			Contract.NotNull(source);
			Contract.NotNull(lambda);

			return Create<TInput, TOutput>(source, (iterator, ct) => new EnumerableIterator<TInput, TOutput>(iterator, lambda, ct));
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
		{
			Contract.NotNull(source);

			return Create<T, T>(source, (iterator, ct) => new EnumerableIterator<T, T>(iterator, x => Task.FromResult(x), ct));
		}

		/// <summary>Wraps an async lambda into an async sequence that will return the result of the lambda</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<T> FromTask<T>(Func<Task<T>> asyncLambda)
		{
			//TODO: create a custom iterator for this ?
			return ToAsyncEnumerable(new [] { asyncLambda }).Select(x => x());
		}

		/// <summary>Split a sequence of items into several batches</summary>
		/// <typeparam name="T">Type of the elements in <paramref name="source"/></typeparam>
		/// <param name="source">Source sequence</param>
		/// <param name="batchSize">Maximum size of each batch</param>
		/// <returns>Sequence of batches, whose size will always we <paramref name="batchSize"/>, except for the last batch that will only hold the remaining items. If the source is empty, an empty sequence is returned.</returns>
		[Pure, LinqTunnel]
		public static IEnumerable<List<T>> Buffered<T>(this IEnumerable<T> source, int batchSize)
		{
			Contract.NotNull(source);
			if (batchSize <= 0) throw new ArgumentException("Batch size must be greater than zero.", nameof(batchSize));

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
			if (list.Count > 0)
			{
				yield return list;
			}
		}

		#endregion

		#region Staying in the Monad...

		#region SelectMany...

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> SelectMany<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.SelectMany<TResult>(selector);
			}

			return Flatten<TSource, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TResult>>(selector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> SelectMany<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<IEnumerable<TResult>>> asyncSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncSelector);

			return SelectMany<TSource, TResult>(source, TaskHelpers.WithCancellation(asyncSelector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> SelectMany<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncSelector);

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.SelectMany<TResult>(asyncSelector);
			}

			return Flatten<TSource, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TResult>>(asyncSelector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.SelectMany<TCollection, TResult>(collectionSelector, resultSelector);
			}

			return Flatten<TSource, TCollection, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TCollection>>(collectionSelector), resultSelector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncCollectionSelector);
			Contract.NotNull(resultSelector);

			return SelectMany<TSource, TCollection, TResult>(source, TaskHelpers.WithCancellation(asyncCollectionSelector), resultSelector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncCollectionSelector);
			Contract.NotNull(resultSelector);

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.SelectMany<TCollection, TResult>(asyncCollectionSelector, resultSelector);
			}

			return Flatten<TSource, TCollection, TResult>(source, new AsyncTransformExpression<TSource,IEnumerable<TCollection>>(asyncCollectionSelector), resultSelector);
		}

		#endregion

		#region Select...

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> Select<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, TResult> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.Select<TResult>(selector);
			}

			return Map<TSource, TResult>(source, new AsyncTransformExpression<TSource,TResult>(selector));
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> Select<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<TResult>> asyncSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncSelector);

			return Select<TSource, TResult>(source, TaskHelpers.WithCancellation(asyncSelector));
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> Select<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> asyncSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncSelector);

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.Select<TResult>(asyncSelector);
			}

			return Map<TSource, TResult>(source, new AsyncTransformExpression<TSource,TResult>(asyncSelector));
		}

		#endregion

		#region Where...

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> Where<TResult>(this IAsyncEnumerable<TResult> source, Func<TResult, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is AsyncIterator<TResult> iterator)
			{
				return iterator.Where(predicate);
			}

			return Filter<TResult>(source, new AsyncFilterExpression<TResult>(predicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<T> Where<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> asyncPredicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncPredicate);

			return Where<T>(source, TaskHelpers.WithCancellation(asyncPredicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> Where<TResult>(this IAsyncEnumerable<TResult> source, Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncPredicate);

			if (source is AsyncIterator<TResult> iterator)
			{
				return iterator.Where(asyncPredicate);
			}

			return Filter<TResult>(source, new AsyncFilterExpression<TResult>(asyncPredicate));
		}

		#endregion

		#region Take...

		/// <summary>Returns a specified number of contiguous elements from the start of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TSource> Take<TSource>(this IAsyncEnumerable<TSource> source, int count)
		{
			Contract.NotNull(source);
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be less than zero");

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.Take(count);
			}

			return Limit<TSource>(source, count);
		}

		#endregion

		#region TakeWhile...

		/// <summary>Returns elements from an async sequence as long as a specified condition is true, and then skips the remaining elements.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TSource> TakeWhile<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> condition)
		{
			Contract.NotNull(source);
			Contract.NotNull(condition);

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.TakeWhile(condition);
			}

			return Limit<TSource>(source, condition);
		}

		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TSource> TakeWhile<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> condition, out QueryStatistics<bool> stopped)
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

		#endregion

		#region Skip...

		/// <summary>Skips the first elements of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TSource> Skip<TSource>(this IAsyncEnumerable<TSource> source, int count)
		{
			Contract.NotNull(source);
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be less than zero");

			if (source is AsyncIterator<TSource> iterator)
			{
				return iterator.Skip(count);
			}

			return Offset<TSource>(source, count);
		}

		#endregion

		#region SelectAsync

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TResult> SelectAsync<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> asyncSelector, ParallelAsyncQueryOptions? options = null)
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
		public static IAsyncEnumerable<TSource> Prefetch<TSource>(this IAsyncEnumerable<TSource> source)
		{
			Contract.NotNull(source);

			return new PrefetchingAsyncIterator<TSource>(source, 1);
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
		public static IAsyncEnumerable<TSource> Prefetch<TSource>(this IAsyncEnumerable<TSource> source, int prefetchCount)
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
		public static IAsyncEnumerable<TSource[]> Window<TSource>(this IAsyncEnumerable<TSource> source, int maxWindowSize)
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
		public static IAsyncEnumerable<TSource[]> Batch<TSource>(this IAsyncEnumerable<TSource> source, int batchSize)
		{
			Contract.NotNull(source);
			if (batchSize <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be at least one.");

			return new BatchingAsyncIterator<TSource>(source, batchSize);
		}

		#endregion

		#region Distinct...

		[Pure, LinqTunnel]
		public static IAsyncEnumerable<TSource> Distinct<TSource>(this IAsyncEnumerable<TSource> source, IEqualityComparer<TSource>? comparer = null)
		{
			Contract.NotNull(source);
			return new DistinctAsyncIterator<TSource>(source, comparer ?? EqualityComparer<TSource>.Default);
		}

		#endregion

		#region OrderBy...

		[Pure, LinqTunnel]
		public static IAsyncOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			return new OrderedSequence<TSource, TKey>(source, keySelector, comparer ?? Comparer<TKey>.Default, descending: false, parent: null);
		}

		[Pure, LinqTunnel]
		public static IAsyncOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			return new OrderedSequence<TSource, TKey>(source, keySelector, comparer ?? Comparer<TKey>.Default, descending: true, parent: null);
		}

		[Pure, LinqTunnel]
		public static IAsyncOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this IAsyncOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			return source.CreateOrderedEnumerable(keySelector, comparer, descending: false);
		}

		[Pure, LinqTunnel]
		public static IAsyncOrderedEnumerable<TSource> ThenByDescending<TSource, TKey>(this IAsyncOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
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
		public static Task ForEachAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Action<T> action, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			if (source is AsyncIterator<T> iterator)
			{
				return iterator.ExecuteAsync(action, ct);
			}
			return Run<T>(source, AsyncIterationHint.All, action, ct);
		}

		/// <summary>Execute an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, Task> asyncAction, CancellationToken ct = default)
		{
			Contract.NotNull(asyncAction);

			if (source is AsyncIterator<T> iterator)
			{
				return iterator.ExecuteAsync(TaskHelpers.WithCancellation(asyncAction), ct);
			}

			return ForEachAsync<T>(source, TaskHelpers.WithCancellation(asyncAction), ct);
		}

		/// <summary>Execute an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, CancellationToken, Task> asyncAction, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncAction);

			if (source is AsyncIterator<T> iterator)
			{
				return iterator.ExecuteAsync(asyncAction, ct);
			}

			return Run<T>(source, AsyncIterationHint.All, asyncAction, ct);
		}

		#region ToList/Array/Dictionary/HashSet...

		/// <summary>Create a list from an async sequence.</summary>
		public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
		{
			Contract.NotNull(source);

			var buffer = new Buffer<T>();
			await ForEachAsync(source, (x) => buffer.Add(x), CancellationToken.None).ConfigureAwait(false);
			return buffer.ToList();
		}

		/// <summary>Create a list from an async sequence.</summary>
		public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct)
		{
			Contract.NotNull(source);

			var buffer = new Buffer<T>();
			await ForEachAsync(source, (x) => buffer.Add(x), ct).ConfigureAwait(false);
			return buffer.ToList();
		}

		/// <summary>Create an array from an async sequence.</summary>
		public static async Task<T[]> ToArrayAsync<T>(this IAsyncEnumerable<T> source)
		{
			Contract.NotNull(source);

			var buffer = new Buffer<T>();
			await ForEachAsync(source, (x) => buffer.Add(x), CancellationToken.None).ConfigureAwait(false);
			return buffer.ToArray();
		}

		/// <summary>Create an array from an async sequence.</summary>
		public static async Task<T[]> ToArrayAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct)
		{
			Contract.NotNull(source);

			var buffer = new Buffer<T>();
			await ForEachAsync(source, (x) => buffer.Add(x), ct).ConfigureAwait(false);
			return buffer.ToArray();
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function and key comparer.</summary>
		public static Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>(this IAsyncEnumerable<TSource> source, [InstantHandle] Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			return AggregateAsync(
				source,
				new Dictionary<TKey, TSource>(comparer ?? EqualityComparer<TKey>.Default),
				(results, x) => { results[keySelector(x)] = x; },
				ct
			);
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function, a comparer, and an element selector function.</summary>
		public static Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>(this IAsyncEnumerable<TSource> source, [InstantHandle] Func<TSource, TKey> keySelector, [InstantHandle] Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(elementSelector);

			return AggregateAsync(
				source,
				new Dictionary<TKey, TElement>(comparer ?? EqualityComparer<TKey>.Default),
				(results, x) => { results[keySelector(x)] = elementSelector(x); },
				ct
			);
		}

		/// <summary>Creates a Dictionary from an async sequence of pairs of keys and values.</summary>
		public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IAsyncEnumerable<KeyValuePair<TKey, TValue>> source, IEqualityComparer<TKey>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			return AggregateAsync(
				source,
				new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default),
				(results, x) => { results[x.Key] = x.Value; },
				ct
			);
		}

		/// <summary>Create an Hashset from an async sequence.</summary>
		public static Task<HashSet<T>> ToHashSetAsync<T>(this IAsyncEnumerable<T> source, IEqualityComparer<T>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			return AggregateAsync(
				source,
				new Buffer<T>(),
				(buffer, x) => buffer.Add(x),
				(buffer) => buffer.ToHashSet(comparer),
				ct
			);
		}

		#endregion

		#region Aggregate...

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TSource> AggregateAsync<TSource>(this IAsyncEnumerable<TSource> source, [InstantHandle] Func<TSource, TSource, TSource> aggregator, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			ct.ThrowIfCancellationRequested();
			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, AsyncIterationHint.All) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The sequence returned a null async iterator");

				if (!(await iterator.MoveNextAsync().ConfigureAwait(false)))
				{
					throw new InvalidOperationException("The sequence was empty");
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
		public static async Task<TAccumulate> AggregateAsync<TSource, TAccumulate>(this IAsyncEnumerable<TSource> source, TAccumulate seed, [InstantHandle] Func<TAccumulate, TSource, TAccumulate> aggregator, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			//TODO: optimize this to not have to allocate lambdas!
			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }, ct).ConfigureAwait(false);
			return accumulate;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TAccumulate> AggregateAsync<TSource, TAccumulate>(this IAsyncEnumerable<TSource> source, TAccumulate seed, [InstantHandle] Action<TAccumulate, TSource> aggregator, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			var accumulate = seed;
			await ForEachAsync(source, (x) => { aggregator(accumulate, x); }, ct).ConfigureAwait(false);
			return accumulate;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAccumulate, TResult>(this IAsyncEnumerable<TSource> source, TAccumulate seed, [InstantHandle] Func<TAccumulate, TSource, TAccumulate> aggregator, [InstantHandle] Func<TAccumulate, TResult> resultSelector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }, ct).ConfigureAwait(false);
			return resultSelector(accumulate);
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAccumulate, TResult>(this IAsyncEnumerable<TSource> source, TAccumulate seed, [InstantHandle] Action<TAccumulate, TSource> aggregator, [InstantHandle] Func<TAccumulate, TResult> resultSelector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			var accumulate = seed;
			await ForEachAsync(source, (x) => aggregator(accumulate, x), ct);
			return resultSelector(accumulate);
		}

		#endregion

		#region First/Last/Single...

		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> FirstAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.FirstAsync();

			return Head<T>(source, single: false, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> FirstAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.FirstAsync();

			//TODO: PERF: custom implementation for this?
			return Head<T>(source.Where(predicate), single: false, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.FirstOrDefaultAsync();

			return Head<T>(source, single: false, orDefault: true, ct: ct);
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.FirstOrDefaultAsync();

			//TODO: PERF: custom implementation for this?
			return Head<T>(source.Where(predicate), single: false, orDefault: true, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.SingleAsync();

			return Head<T>(source, single: true, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.SingleAsync();

			//TODO: PERF: custom implementation for this?
			return Head<T>(source.Where(predicate), single: true, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.SingleOrDefaultAsync();

			return Head<T>(source, single: true, orDefault: true, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return rq.SingleOrDefaultAsync();

			return Head<T>(source.Where(predicate), single: true, orDefault: true, ct: ct);
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static async Task<T> LastAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return await rq.LastAsync();

			bool found = false;
			T last = default!;

			await ForEachAsync<T>(source, (x) => { found = true; last = x; }, ct).ConfigureAwait(false);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return last;
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static async Task<T> LastAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return await rq.LastAsync();

			bool found = false;
			T last = default!;

			await ForEachAsync<T>(source, (x) => { if (predicate(x)) { found = true; last = x; } }, ct).ConfigureAwait(false);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return last;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefaultAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return await rq.LastOrDefaultAsync();

			bool found = false;
			T last = default!;

			await ForEachAsync<T>(source, (x) => { found = true; last = x; }, ct).ConfigureAwait(false);

			return found ? last : default!;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefaultAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return await rq.LastOrDefaultAsync();

			bool found = false;
			T last = default!;

			await ForEachAsync<T>(source, (x) => { if (predicate(x)) { found = true; last = x; } }, ct).ConfigureAwait(false);

			return found ? last : default!;
		}

		/// <summary>Returns the element at a specific location of an async sequence, or an exception if there are not enough elements</summary>
		public static async Task<T> ElementAtAsync<T>(this IAsyncEnumerable<T> source, int index, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return await rq.Skip(index).SingleAsync();

			int counter = index;
			T item = default!;
			await Run<T>(
				source,
				AsyncIterationHint.All,
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
		public static async Task<T> ElementAtOrDefaultAsync<T>(this IAsyncEnumerable<T> source, int index, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
			ct.ThrowIfCancellationRequested();

			//TODO:REFACTORING: create some interface or base class for this?
			//var rq = source as FdbRangeQuery<T>;
			//if (rq != null) return await rq.Skip(index).SingleAsync();

			int counter = index;
			T item = default!;

			//TODO: use ExecuteAsync() if the source is an Iterator!
			await Run<T>(
				source,
				AsyncIterationHint.All,
				(x) =>
				{
					if (counter-- == 0) { item = x; return false; }
					return true;
				},
				ct
			).ConfigureAwait(false);

			if (counter >= 0) return default!;
			return item;
		}

		#endregion

		#region Count/Sum...

		/// <summary>Returns the number of elements in an async sequence.</summary>
		public static async Task<int> CountAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			int count = 0;

			await ForEachAsync<T>(source, (_) => { ++count; }, ct).ConfigureAwait(false);

			return count;
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static async Task<int> CountAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			int count = 0;

			await ForEachAsync<T>(source, (x) => { if (predicate(x)) ++count; }, ct).ConfigureAwait(false);

			return count;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<uint> SumAsync(this IAsyncEnumerable<uint> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);

			return AggregateAsync(source, 0U, (sum, x) => checked(sum + x), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<uint> SumAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, uint> selector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return AggregateAsync(source, 0U, (sum, x) => checked(sum + selector(x)), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<ulong> SumAsync(this IAsyncEnumerable<ulong> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);

			return AggregateAsync(source, 0UL, (sum, x) => checked(sum + x), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<ulong> SumAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, ulong> selector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return AggregateAsync(source, 0UL, (sum, x) => checked(sum + selector(x)), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<int> SumAsync(this IAsyncEnumerable<int> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);

			return AggregateAsync(source, 0, (sum, x) => checked(sum + x), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<int> SumAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, int> selector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return AggregateAsync(source, 0, (sum, x) => checked(sum + selector(x)), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<long> SumAsync(this IAsyncEnumerable<long> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);

			return AggregateAsync(source, 0L, (sum, x) => checked(sum + x), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<long> SumAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, long> selector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return AggregateAsync(source, 0L, (sum, x) => checked(sum + selector(x)), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<float> SumAsync(this IAsyncEnumerable<float> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);

			return AggregateAsync(source, 0.0f, (sum, x) => sum + x, ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<float> SumAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, float> selector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return AggregateAsync(source, 0.0f, (sum, x) => sum + selector(x), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<double> SumAsync(this IAsyncEnumerable<double> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);

			return AggregateAsync(source, 0.0, (sum, x) => sum + x, ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<double> SumAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, double> selector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return AggregateAsync(source, 0.0, (sum, x) => sum + selector(x), ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<decimal> SumAsync(this IAsyncEnumerable<decimal> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);

			return AggregateAsync(source, 0m, (sum, x) => sum + x, ct);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<decimal> SumAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, decimal> selector, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return AggregateAsync(source, 0m, (sum, x) => sum + selector(x), ct);
		}

		#endregion

		#region Min/Max...

		/// <summary>Returns the smallest value in the specified async sequence</summary>
		public static async Task<T> MinAsync<T>(this IAsyncEnumerable<T> source, IComparer<T>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			comparer ??= Comparer<T>.Default;

			//REVIEW: use C#7 tuples
			bool found = false;
			T min = default!;

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

			if (!found) throw ThrowHelper.InvalidOperationException("The sequence was empty");
			return min;
		}

		/// <summary>Returns the smallest value in the specified async sequence</summary>
		public static async Task<T> MinAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, IComparer<T>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			comparer ??= Comparer<T>.Default;

			//REVIEW: use C#7 tuples
			bool found = false;
			T min = default!;

			await ForEachAsync<T>(
				source,
				(x) =>
				{
					if (predicate(x) && (!found || comparer.Compare(x, min) < 0))
					{
						min = x;
						found = true;
					}
				},
				ct
			).ConfigureAwait(false);

			if (!found) throw ThrowHelper.InvalidOperationException("The sequence was empty");
			return min;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<T> MaxAsync<T>(this IAsyncEnumerable<T> source, IComparer<T>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			comparer ??= Comparer<T>.Default;

			//REVIEW: use C#7 tuples
			bool found = false;
			T max = default!;

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

			if (!found) throw ThrowHelper.InvalidOperationException("The sequence was empty");
			return max;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<T> MaxAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, IComparer<T>? comparer = null, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			comparer ??= Comparer<T>.Default;

			//REVIEW: use C#7 tuples
			bool found = false;
			T max = default!;

			await ForEachAsync<T>(
				source,
				(x) =>
				{
					if (predicate(x) && (!found || comparer.Compare(x, max) > 0))
					{
						max = x;
						found = true;
					}
				},
				ct
			).ConfigureAwait(false);

			if (!found) throw ThrowHelper.InvalidOperationException("The sequence was empty");
			return max;
		}

		#endregion

		#region Any/None...

		/// <summary>Determines whether an async sequence contains any elements.</summary>
		/// <remarks>This is the logical equivalent to "source.Count() > 0" but can be better optimized by some providers</remarks>
		public static async Task<bool> AnyAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<T> configurable ? configurable.GetAsyncEnumerator(ct, AsyncIterationHint.Head) : source.GetAsyncEnumerator(ct))
			{
				return await iterator.MoveNextAsync().ConfigureAwait(false);
			}
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static async Task<bool> AnyAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<T> configurable ? configurable.GetAsyncEnumerator(ct, AsyncIterationHint.Head) : source.GetAsyncEnumerator(ct))
			{
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (predicate(iterator.Current)) return true;
				}
			}
			return false;
		}

		/// <summary>Determines whether an async sequence contains no elements at all.</summary>
		/// <remarks>This is the logical equivalent to "source.Count() == 0" or "!source.Any()" but can be better optimized by some providers</remarks>
		public static async Task<bool> NoneAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<T> configurable ? configurable.GetAsyncEnumerator(ct, AsyncIterationHint.Head) : source.GetAsyncEnumerator(ct))
			{
				return !(await iterator.MoveNextAsync().ConfigureAwait(false));
			}
		}

		/// <summary>Determines whether none of the elements of an async sequence satisfies a condition.</summary>
		public static async Task<bool> NoneAsync<T>(this IAsyncEnumerable<T> source, [InstantHandle] Func<T, bool> predicate, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);
			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<T> configurable ? configurable.GetAsyncEnumerator(ct, AsyncIterationHint.Head) : source.GetAsyncEnumerator(ct))
			{
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (predicate(iterator.Current)) return false;
				}
			}
			return true;
		}

		#endregion

		#endregion

		#region Query Statistics...

		//TODO: move this somewhere else?

		/// <summary>Measure the number of items that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="counter"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IAsyncEnumerable<TSource> WithCountStatistics<TSource>(this IAsyncEnumerable<TSource> source, out QueryStatistics<int> counter)
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
		public static IAsyncEnumerable<KeyValuePair<Slice, Slice>> WithSizeStatistics(this IAsyncEnumerable<KeyValuePair<Slice, Slice>> source, out QueryStatistics<KeyValueSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new KeyValueSizeStatistics();
			statistics = new QueryStatistics<KeyValueSizeStatistics>(data);

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
		public static IAsyncEnumerable<Slice> WithSizeStatistics(this IAsyncEnumerable<Slice> source, out QueryStatistics<DataSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new DataSizeStatistics();
			statistics = new QueryStatistics<DataSizeStatistics>(data);

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
		public static IAsyncEnumerable<TSource> Observe<TSource>(this IAsyncEnumerable<TSource> source, Action<TSource> handler)
		{
			Contract.NotNull(source);
			Contract.NotNull(handler);

			return new ObserverAsyncIterator<TSource>(source, new AsyncObserverExpression<TSource>(handler));
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="asyncHandler"/> is execute inline before passing the item down the line, and should not block</remarks>
		[LinqTunnel]
		public static IAsyncEnumerable<TSource> Observe<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task> asyncHandler)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncHandler);

			return new ObserverAsyncIterator<TSource>(source, new AsyncObserverExpression<TSource>(asyncHandler));
		}

		#endregion

	}
}

#endif
