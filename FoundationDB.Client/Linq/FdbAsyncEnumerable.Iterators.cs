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
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class FdbAsyncEnumerable
	{

		#region Create...

		/// <summary>Create a new async sequence that will transform an inner async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source async sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static AsyncSequence<TSource, TResult> Create<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<IFdbAsyncEnumerator<TSource>, IFdbAsyncEnumerator<TResult>> factory)
		{
			return new AsyncSequence<TSource, TResult>(source, factory);
		}

		/// <summary>Create a new async sequence that will transform an inner sequence</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static EnumerableSequence<TSource, TResult> Create<TSource, TResult>(IEnumerable<TSource> source, Func<IEnumerator<TSource>, IFdbAsyncEnumerator<TResult>> factory)
		{
			return new EnumerableSequence<TSource, TResult>(source, factory);
		}

		#endregion

		#region Flatten...

		internal static SelectManyAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			return new SelectManyAsyncIterator<TSource, TResult>(source, selector, null);
		}

		internal static SelectManyAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
		{
			return new SelectManyAsyncIterator<TSource, TResult>(source, null, asyncSelector);
		}

		internal static SelectManyAsyncIterator<TSource, TCollection, TResult> Flatten<TSource, TCollection, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			return new SelectManyAsyncIterator<TSource, TCollection, TResult>(
				source,
				collectionSelector,
				null,
				resultSelector
			);
		}

		internal static SelectManyAsyncIterator<TSource, TCollection, TResult> Flatten<TSource, TCollection, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			return new SelectManyAsyncIterator<TSource, TCollection, TResult>(
				source,
				null,
				asyncCollectionSelector,
				resultSelector
			);
		}

		#endregion

		#region Map...

		internal static WhereSelectAsyncIterator<TSource, TResult> Map<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, TResult> selector, int? limit = null)
		{
			return new WhereSelectAsyncIterator<TSource, TResult>(source, filter: null, asyncFilter: null, transform: selector, asyncTransform: null, limit: limit);
		}
		internal static WhereSelectAsyncIterator<TSource, TResult> Map<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> asyncSelector, int? limit = null)
		{
			return new WhereSelectAsyncIterator<TSource, TResult>(source, filter: null, asyncFilter: null, transform: null, asyncTransform: asyncSelector, limit: limit);
		}

		#endregion

		#region Filter...

		internal static WhereAsyncIterator<TResult> Filter<TResult>(IFdbAsyncEnumerable<TResult> source, Func<TResult, bool> predicate)
		{
			return new WhereAsyncIterator<TResult>(source, predicate, null);
		}

		internal static WhereAsyncIterator<TResult> Filter<TResult>(IFdbAsyncEnumerable<TResult> source, Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			return new WhereAsyncIterator<TResult>(source, null, asyncPredicate);
		}

		#endregion

		#region Limit...

		internal static WhereSelectAsyncIterator<TResult, TResult> Limit<TResult>(IFdbAsyncEnumerable<TResult> source, int limit)
		{
			return new WhereSelectAsyncIterator<TResult, TResult>(source, filter: null, asyncFilter: null, transform: TaskHelpers.Cache<TResult>.Identity, asyncTransform: null, limit: limit);
		}

		#endregion

		#region Run...

		/// <summary>Immediately execute an action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(IFdbAsyncEnumerable<TSource> source, Action<TSource> action, CancellationToken ct)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			ct.ThrowIfCancellationRequested();

			long count = 0;
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct).ConfigureAwait(false))
				{
					action(iterator.Current);
					++count;
				}
			}
			return count;
		}

		/// <summary>Immediately execute an asunc action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(IFdbAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task> action, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long count = 0;
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct).ConfigureAwait(false))
				{
					await action(iterator.Current, ct).ConfigureAwait(false);
					++count;
				}
			}
			return count;
		}

		/// <summary>Immediately execute an asunc action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(IFdbAsyncEnumerable<TSource> source, Func<TSource, Task> action, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long count = 0;
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct).ConfigureAwait(false))
				{
					ct.ThrowIfCancellationRequested();
					await action(iterator.Current).ConfigureAwait(false);
					++count;
				}
			}
			return count;
		}

		/// <summary>Helper async method to get the first element of an async sequence</summary>
		/// <typeparam name="T">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="single">If true, the sequence must contain at most one element</param>
		/// <param name="orDefault">When the sequence is empty: If true then returns the default value for the type. Otherwise, throws an exception</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Value of the first element of the <paramref="source"/> sequence, or the default value, or an exception (depending on <paramref name="single"/> and <paramref name="orDefault"/></returns>
		internal static async Task<TSource> Head<TSource>(IFdbAsyncEnumerable<TSource> source, bool single, bool orDefault, CancellationToken ct)
		{
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The sequence returned a null async iterator");

				if (await iterator.MoveNext(ct).ConfigureAwait(false))
				{
					TSource first = iterator.Current;
					if (single)
					{
						if (await iterator.MoveNext(ct).ConfigureAwait(false)) throw new InvalidOperationException("The sequence contained more than one element");
					}
					return first;
				}
				if (!orDefault) throw new InvalidOperationException("The sequence was empty");
				return default(TSource);
			}
		}

		#endregion

	}
}
