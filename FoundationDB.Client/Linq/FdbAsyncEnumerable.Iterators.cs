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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
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
		internal static FdbAsyncSequence<TSource, TResult> Create<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<IFdbAsyncEnumerator<TSource>, IFdbAsyncEnumerator<TResult>> factory)
		{
			return new FdbAsyncSequence<TSource, TResult>(source, factory);
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

		/// <summary>Create a new async sequence from a factory method</summary>
		public static IFdbAsyncEnumerable<TResult> Create<TResult>(Func<object, IFdbAsyncEnumerator<TResult>> factory, object state = null)
		{
			return new AnonymousIterable<TResult>(factory, state);
		}

		internal sealed class AnonymousIterable<T> : IFdbAsyncEnumerable<T>
		{

			private readonly Func<object, IFdbAsyncEnumerator<T>> m_factory;
			private readonly object m_state;

			public AnonymousIterable(Func<object, IFdbAsyncEnumerator<T>> factory, object state)
			{
				m_factory = factory;
				m_state = state;
			}

			public IAsyncEnumerator<T> GetEnumerator()
			{
				return this.GetEnumerator(FdbAsyncMode.Default);
			}

			public IFdbAsyncEnumerator<T> GetEnumerator(FdbAsyncMode _)
			{
				return m_factory(m_state);
			}
		}

		#endregion

		#region Flatten...

		internal static FdbSelectManyAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			return new FdbSelectManyAsyncIterator<TSource, TResult>(source, selector, null);
		}

		internal static FdbSelectManyAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
		{
			return new FdbSelectManyAsyncIterator<TSource, TResult>(source, null, asyncSelector);
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

		internal static FdbWhereSelectAsyncIterator<TSource, TResult> Map<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, TResult> selector, int? limit = null)
		{
			return new FdbWhereSelectAsyncIterator<TSource, TResult>(source, filter: null, asyncFilter: null, transform: selector, asyncTransform: null, limit: limit);
		}
		internal static FdbWhereSelectAsyncIterator<TSource, TResult> Map<TSource, TResult>(IFdbAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, Task<TResult>> asyncSelector, int? limit = null)
		{
			return new FdbWhereSelectAsyncIterator<TSource, TResult>(source, filter: null, asyncFilter: null, transform: null, asyncTransform: asyncSelector, limit: limit);
		}

		#endregion

		#region Filter...

		internal static FdbWhereAsyncIterator<TResult> Filter<TResult>(IFdbAsyncEnumerable<TResult> source, Func<TResult, bool> predicate)
		{
			return new FdbWhereAsyncIterator<TResult>(source, predicate, null);
		}

		internal static FdbWhereAsyncIterator<TResult> Filter<TResult>(IFdbAsyncEnumerable<TResult> source, Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			return new FdbWhereAsyncIterator<TResult>(source, null, asyncPredicate);
		}

		#endregion

		#region Limit...

		internal static FdbWhereSelectAsyncIterator<TResult, TResult> Limit<TResult>(IFdbAsyncEnumerable<TResult> source, int limit)
		{
			return new FdbWhereSelectAsyncIterator<TResult, TResult>(source, filter: null, asyncFilter: null, transform: TaskHelpers.Cache<TResult>.Identity, asyncTransform: null, limit: limit);
		}

		internal static FdbTakeWhileAsyncIterator<TResult> Limit<TResult>(IFdbAsyncEnumerable<TResult> source, Func<TResult, bool> condition)
		{
			return new FdbTakeWhileAsyncIterator<TResult>(source, condition);
		}

		#endregion

		#region Run...

		/// <summary>Small buffer that keeps a list of chunks that are larger and larger</summary>
		/// <typeparam name="T">Type of elements stored in the buffer</typeparam>
		[DebuggerDisplay("Count={Count}, Chunks={this.Chunks.Length}, Current={Index}/{Current.Length}")]
		internal class Buffer<T>
		{
			// We want to avoid growing the same array again and again !
			// Instead, we grow list of chunks, that grow in size (until a max), and concatenate all the chunks together at the end, once we know the final size

			/// <summary>Default intial capacity, if not specified</summary>
			const int DefaultCapacity = 16;
			//REVIEW: should we use a power of 2 or of 10 for initial capacity? 
			// Since humans prefer the decimal system, it is more likely that query limit count be set to something like 10, 50, 100 or 1000
			// but most "human friendly" limits are close to the next power of 2, like 10 ~= 16, 50 ~= 64, 100 ~= 128, 500 ~= 512, 1000 ~= 1024, so we don't waste that much space...

			/// <summary>Maximum size of a chunk</summary>
			const int MaxChunkSize = 4096;

			/// <summary>Number of items in the buffer</summary>
			public int Count;
			/// <summary>Index in the current chunk</summary>
			public int Index;
			/// <summary>List of chunks</summary>
			public T[][] Chunks;
			/// <summary>Current (and last) chunk</summary>
			public T[] Current;

			public Buffer(int capacity = 0)
			{
				if (capacity <= 0) capacity = DefaultCapacity;

				this.Count = 0;
				this.Index = 0;
				this.Chunks = new T[1][];
				this.Current = new T[capacity];
				this.Chunks[0] = this.Current;
			}

			public void Add(T item)
			{
				if (this.Index == this.Current.Length)
				{
					Grow();
				}

				checked { ++this.Count; }
				this.Current[this.Index++] = item;
			}

			private void Grow()
			{
				// Growth rate:
				// - newly created chunk is always half the total size
				// - except the first chunk who is set to the inital capacity

				Array.Resize(ref this.Chunks, this.Chunks.Length + 1);
				this.Current = new T[Math.Min(this.Count, MaxChunkSize)];
				this.Chunks[this.Chunks.Length - 1] = this.Current;
				this.Index = 0;
			}

			public T[] ToArray()
			{
				if (this.Count == 0)
				{ // empty sequence
					return new T[0];
				}
				else if (this.Chunks.Length == 1 && this.Current.Length == this.Count)
				{ // we are really lucky
					return this.Current;
				}
				else
				{ // concatenate all the small buffers into one big array
					var tmp = new T[this.Count];
					int count = this.Count;
					int index = 0;
					for (int i = 0; i < this.Chunks.Length - 1;i++)
					{
						var chunk = this.Chunks[i];
						Array.Copy(chunk, 0, tmp, index, chunk.Length);
						index += chunk.Length;
						count -= chunk.Length;
					}
					Array.Copy(this.Current, 0, tmp, index, count);
					return tmp;
				}
			}

			public List<T> ToList()
			{
				if (this.Count == 0)
				{ // empty sequence
					return new List<T>();
				}

				int count = this.Count;
				var list = new List<T>(count);
				if (count > 0)
				{
					for (int i = 0; i < this.Chunks.Length - 1; i++)
					{
						list.AddRange(this.Chunks[i]);
						count -= this.Chunks[i].Length;
					}

					if (count == this.Current.Length)
					{ // the last chunk fits perfectly
						list.AddRange(this.Current);
					}
					else
					{ // there is not AddRange(buffer, offset, count) on List<T>, so we copy to a tmp array and AddRange
						var tmp = new T[count];
						Array.Copy(this.Current, tmp, count);
						list.AddRange(tmp);
					}
				}

				return list;
			}
		}

		/// <summary>Immediately execute an action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">If different than default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(IFdbAsyncEnumerable<TSource> source, FdbAsyncMode mode, Action<TSource> action, CancellationToken ct)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			ct.ThrowIfCancellationRequested();

			//note: we should not use "ConfigureAwait(false)" here because we would like to execute the action in the original synchronization context if possible...

			long count = 0;
			using (var iterator = source.GetEnumerator(mode))
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct))
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
		/// <param name="mode">Expected execution mode of the query</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(IFdbAsyncEnumerable<TSource> source, FdbAsyncMode mode, Func<TSource, CancellationToken, Task> action, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			//note: we should not use "ConfigureAwait(false)" here because we would like to execute the action in the original synchronization context if possible...

			long count = 0;
			using (var iterator = source.GetEnumerator(mode))
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct))
				{
					await action(iterator.Current, ct);
					++count;
				}
			}
			return count;
		}

		/// <summary>Immediately execute an asunc action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">Expected execution mode of the query</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(IFdbAsyncEnumerable<TSource> source, FdbAsyncMode mode, Func<TSource, Task> action, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			//note: we should not use "ConfigureAwait(false)" here because we would like to execute the action in the original synchronization context if possible...

			long count = 0;
			using (var iterator = source.GetEnumerator(mode))
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct))
				{
					ct.ThrowIfCancellationRequested();
					await action(iterator.Current);
					++count;
				}
			}
			return count;
		}

		/// <summary>Helper async method to get the first element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="single">If true, the sequence must contain at most one element</param>
		/// <param name="orDefault">When the sequence is empty: If true then returns the default value for the type. Otherwise, throws an exception</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Value of the first element of the <param ref="source"/> sequence, or the default value, or an exception (depending on <paramref name="single"/> and <paramref name="orDefault"/></returns>
		internal static async Task<TSource> Head<TSource>(IFdbAsyncEnumerable<TSource> source, bool single, bool orDefault, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			//note: we should not use "ConfigureAwait(false)" here because we would like to execute the action in the original synchronization context if possible...

			using (var iterator = source.GetEnumerator(FdbAsyncMode.Head))
			{
				if (iterator == null) throw new InvalidOperationException("The sequence returned a null async iterator");

				if (await iterator.MoveNext(ct))
				{
					TSource first = iterator.Current;
					if (single)
					{
						if (await iterator.MoveNext(ct)) throw new InvalidOperationException("The sequence contained more than one element");
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
