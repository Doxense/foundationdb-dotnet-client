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
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class FdbAsyncEnumerable
	{
		// Welcome to the wonderful world of the Monads! 

		/// <summary>Helper class that will hold on cached generic delegates</summary>
		/// <typeparam name="T"></typeparam>
		private static class Cache<T>
		{
			public static readonly Func<T, T> Identity = (x) => x;
		}

		#region Entering the Monad...

		/// <summary>Returns an empty async sequence</summary>
		public static IFdbAsyncEnumerable<T> Empty<T>()
		{
			return EmptySequence<T>.Default;
		}

		/// <summary>Returns an async sequence that only holds one item</summary>
		public static IFdbAsyncEnumerable<T> Singleton<T>(T value)
		{
			//TODO: implement an optimized singleton iterator ?
			return new T[1] { value }.ToAsyncEnumerable();
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		public static IFdbAsyncEnumerable<R> ToAsyncEnumerable<T, R>(this IEnumerable<T> source, Func<T, Task<R>> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return new EnumerableSequence<T, R>(source, (iterator) => new EnumerableIterator<T, R>(iterator, lambda));
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		public static IFdbAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			return new EnumerableSequence<T, T>(source, (iterator) => new EnumerableIterator<T, T>(iterator, x => Task.FromResult(x)));
		}

		/// <summary>Wraps an async lambda into an async sequence that will return the result of the lambda</summary>
		public static IFdbAsyncEnumerable<T> FromTask<T>(Func<Task<T>> asyncLambda)
		{
			//TODO: create a custom iterator for this ?
			return ToAsyncEnumerable(new [] { asyncLambda }).Select(x => x());
		}

		#endregion

		#region Staying in the Monad...

		/// <summary>Projects each element of an async sequence to an IFdbAsyncEnumerable&lt;T&gt; and flattens the resulting sequences into one async sequence.</summary>
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			return Create<TSource, TResult>(source, (iterator) =>
			{
				return Flatten<TSource, TResult>(iterator, selector);
			});
		}

		/// <summary>Projects each element of an async sequence to an IFdbAsyncEnumerable&lt;T&gt; flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		public static IFdbAsyncEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			return Create<TSource, TResult>(source, (iterator) =>
			{
				return Flatten<TSource, TCollection, TResult>(iterator, collectionSelector, resultSelector);
			});
		}

		#region Select...

		/// <summary>Projects each element of a async sequence into a new form.</summary>
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, TResult> selector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			return Create(source, (iterator) => Map<TSource, TResult>(iterator, null, selector));
		}

		/// <summary>Projects each element of an async sequence into a new form by incorporating the element's index.</summary>
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, int, TResult> indexedSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (indexedSelector == null) throw new ArgumentNullException("indexedSelector");

			return Create<TSource, TResult>(source, (iterator) =>
			{
				int index = 0;
				return Map<TSource, TResult>(iterator, null, (x) => indexedSelector(x, index++));
			});		
		}

		/// <summary>Projects each element of a async sequence into a new form.</summary>
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, Task<TResult>> asyncSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return Create<TSource, TResult>(source, (iterator) => Map<TSource, TResult>(iterator, null, WithCancellation(asyncSelector)));
		}

		/// <summary>Projects each element of a async sequence into a new form.</summary>
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> asyncSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return Create<TSource, TResult>(source, (iterator) => Map<TSource, TResult>(iterator, null, asyncSelector));
		}

		/// <summary>Projects each element of an async sequence into a new form by incorporating the element's index.</summary>
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, int, Task<TResult>> asyncIndexedSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncIndexedSelector == null) throw new ArgumentNullException("asyncIndexedSelector");

			return Create<TSource, TResult>(source, (iterator) =>
			{
				int index = 0;
				return Map<TSource, TResult>(
					iterator,
					null,
					(x, ct) =>
					{
						ct.ThrowIfCancellationRequested();
						return asyncIndexedSelector(x, index++);
					}
				);
			});
		}

		/// <summary>Projects each element of an async sequence into a new form by incorporating the element's index.</summary>
		public static IFdbAsyncEnumerable<TResult> Select<TSource, TResult>(this IFdbAsyncEnumerable<TSource> source, Func<TSource, int, CancellationToken, Task<TResult>> asyncIndexedSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (asyncIndexedSelector == null) throw new ArgumentNullException("asyncIndexedSelector");

			return Create<TSource, TResult>(source, (iterator) =>
			{
				int index = 0;
				return Map<TSource, TResult>(iterator, null, (x, ct) => asyncIndexedSelector(x, index++, ct));
			});
		}

		#endregion

		#region Where...

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		public static IFdbAsyncEnumerable<T> Where<T>(this IFdbAsyncEnumerable<T> source, Func<T, bool> predicate)
		{
			return Create<T, T>(source, (iterator) => Filter<T>(iterator, predicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate. Each element's index is used in the logic of the predicate function.</summary>
		public static IFdbAsyncEnumerable<T> Where<T>(this IFdbAsyncEnumerable<T> source, Func<T, int, bool> predicate)
		{
			return Create<T, T>(source, (iterator) =>
			{
				int index = 0;
				return Filter<T>(iterator, (x) => predicate(x, index++));
			});
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		public static IFdbAsyncEnumerable<T> Where<T>(this IFdbAsyncEnumerable<T> source, Func<T, Task<bool>> asyncPredicate)
		{
			return Create<T, T>(source, (iterator) => Filter<T>(iterator, WithCancellation(asyncPredicate)));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		public static IFdbAsyncEnumerable<T> Where<T>(this IFdbAsyncEnumerable<T> source, Func<T, CancellationToken, Task<bool>> asyncPredicate)
		{
			return Create<T, T>(source, (iterator) => Filter<T>(iterator, asyncPredicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		public static IFdbAsyncEnumerable<T> Where<T>(this IFdbAsyncEnumerable<T> source, Func<T, int, Task<bool>> asyncPredicate)
		{
			return Create<T, T>(source, (iterator) =>
			{
				int index = 0;
				return Filter<T>(iterator, (value, ct) =>
				{
					ct.ThrowIfCancellationRequested();
					return asyncPredicate(value, index++);
				});
			});
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		public static IFdbAsyncEnumerable<T> Where<T>(this IFdbAsyncEnumerable<T> source, Func<T, int, CancellationToken, Task<bool>> asyncPredicate)
		{
			return Create<T, T>(source, (iterator) =>
			{
				int index = 0;
				return Filter<T>(iterator, (value, ct) => asyncPredicate(value, index++, ct));
			});
		}

		#endregion

		#endregion

		#region Leaving the Monad...

		/// <summary>Execute an action for each element of an async sequence</summary>
		public static Task ForEachAsync<T>(this IFdbAsyncEnumerable<T> source, Action<T> action, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			return Run<T>(source, action, ct);
		}

		/// <summary>Execute an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<T>(this IFdbAsyncEnumerable<T> source, Func<T, Task> action, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			return Run<T>(source, action, ct);
		}

		/// <summary>Return a list of all the elements from an async sequence</summary>
		public static async Task<List<T>> ToListAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			var list = new List<T>();
			await Run<T>(source, (x) => list.Add(x), ct);
			return list;
		}
		
		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> FirstAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: false, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefaultAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: false, orDefault: true, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: true, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: true, orDefault: true, ct: ct);
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static async Task<T> LastAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			bool found = false;
			T last = default(T);

			await Run<T>(source, (x) => { found = true; last = x; }, ct);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return last;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefaultAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			bool found = false;
			T last = default(T);

			await Run<T>(source, (x) => { found = true; last = x; }, ct);

			return found ? last : default(T);
		}

		/// <summary>Returns the number of elements in an async sequence.</summary>
		public static async Task<int> CountAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			int count = 0;

			await Run<T>(source, (_) => { ++count; }, ct);

			return count;
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static async Task<int> CountAsync<T>(this IFdbAsyncEnumerable<T> source, Func<T, bool> predicate, CancellationToken ct = default(CancellationToken))
		{
			if (predicate == null) throw new ArgumentNullException("predicate");

			int count = 0;

			await Run<T>(source, (x) =>
			{ 
				if (predicate(x)) ++count; 
			}, ct);

			return count;
		}

		/// <summary>Determines whether an async sequence contains any elements.</summary>
		public static async Task<bool> AnyAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");

			ct.ThrowIfCancellationRequested();

			using (var iterator = source.GetEnumerator())
			{
				return await iterator.MoveNext(ct);
			}
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static async Task<bool> AnyAsync<T>(this IFdbAsyncEnumerable<T> source, Func<T, bool> predicate, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");

			ct.ThrowIfCancellationRequested();

			using (var iterator = source.GetEnumerator())
			{
				while (await iterator.MoveNext(ct))
				{
					if (predicate(iterator.Current)) return true;
				}
			}
			return false;
		}

		#endregion

	}

}
