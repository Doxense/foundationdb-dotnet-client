#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Linq
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq.Async;
	using Doxense.Linq.Async.Expressions;
	using Doxense.Linq.Async.Iterators;
	using JetBrains.Annotations;

	public static partial class AsyncEnumerable
	{

		#region Create...

		/// <summary>Create a new async sequence that will transform an inner async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source async sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static AsyncSequence<TSource, TResult> Create<TSource, TResult>(
			IAsyncEnumerable<TSource> source,
			Func<IAsyncEnumerator<TSource>,
			IAsyncEnumerator<TResult>> factory)
		{
			return new AsyncSequence<TSource, TResult>(source, factory);
		}

		/// <summary>Create a new async sequence that will transform an inner sequence</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static EnumerableSequence<TSource, TResult> Create<TSource, TResult>(
			IEnumerable<TSource> source,
			Func<IEnumerator<TSource>, CancellationToken, IAsyncEnumerator<TResult>> factory)
		{
			return new EnumerableSequence<TSource, TResult>(source, factory);
		}

		/// <summary>Create a new async sequence from a factory method</summary>
		public static IAsyncEnumerable<TResult> Create<TResult>(
			Func<object?, CancellationToken, IAsyncEnumerator<TResult>> factory,
			object? state = null)
		{
			return new AnonymousIterable<TResult>(factory, state);
		}

		internal sealed class AnonymousIterable<T> : IAsyncEnumerable<T>
		{

			private readonly Func<object?, CancellationToken, IAsyncEnumerator<T>> m_factory;
			private readonly object? m_state;

			public AnonymousIterable(Func<object?, CancellationToken, IAsyncEnumerator<T>> factory, object? state)
			{
				Contract.Debug.Requires(factory != null);
				m_factory = factory;
				m_state = state;
			}

			public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
			{
				return m_factory(m_state, ct);
			}

			public IAsyncEnumerator<T> GetEnumerator(CancellationToken ct, AsyncIterationHint _)
			{
				ct.ThrowIfCancellationRequested();
				return m_factory(m_state, ct);
			}
		}

		/// <summary>Create a new async sequence from a factory that will generated on the first iteration</summary>
		public static IConfigurableAsyncEnumerable<TResult> Defer<TResult>(Func<CancellationToken, Task<IAsyncEnumerable<TResult>>> factory)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncIterator<TResult, IAsyncEnumerable<TResult>>(factory);
		}

		/// <summary>Create a new async sequence from a factory that will generated on the first iteration</summary>
		public static IConfigurableAsyncEnumerable<TResult> Defer<TResult, TCollection>(Func<CancellationToken, Task<TCollection>> factory)
			where TCollection: IAsyncEnumerable<TResult>
		{
			Contract.NotNull(factory);
			return new DeferredAsyncIterator<TResult, TCollection>(factory);
		}

		#endregion

		#region Helpers...

		internal static SelectManyAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(
			IAsyncEnumerable<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TResult>> selector)
		{
			return new SelectManyAsyncIterator<TSource, TResult>(source, selector);
		}

		internal static SelectManyAsyncIterator<TSource, TCollection, TResult> Flatten<TSource, TCollection, TResult>(
			IAsyncEnumerable<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TCollection>> collectionSelector,
			Func<TSource, TCollection, TResult> resultSelector)
		{
			return new SelectManyAsyncIterator<TSource, TCollection, TResult>(
				source,
				collectionSelector,
				resultSelector
			);
		}

		internal static WhereSelectAsyncIterator<TSource, TResult> Map<TSource, TResult>(
			IAsyncEnumerable<TSource> source,
			AsyncTransformExpression<TSource, TResult> selector,
			int? limit = null, int?
			offset = null)
		{
			return new WhereSelectAsyncIterator<TSource, TResult>(source, filter: null, transform: selector, limit: limit, offset: offset);
		}

		internal static WhereAsyncIterator<TResult> Filter<TResult>(
			IAsyncEnumerable<TResult> source,
			AsyncFilterExpression<TResult> filter)
		{
			return new WhereAsyncIterator<TResult>(source, filter);
		}

		internal static WhereSelectAsyncIterator<TResult, TResult> Offset<TResult>(
			IAsyncEnumerable<TResult> source,
			int offset)
		{
			return new WhereSelectAsyncIterator<TResult, TResult>(source, filter: null, transform: new AsyncTransformExpression<TResult, TResult>(), limit: null, offset: offset);
		}

		internal static WhereSelectAsyncIterator<TResult, TResult> Limit<TResult>(
			IAsyncEnumerable<TResult> source,
			int limit)
		{
			return new WhereSelectAsyncIterator<TResult, TResult>(source, filter: null, transform: new AsyncTransformExpression<TResult, TResult>(), limit: limit, offset: null);
		}

		internal static TakeWhileAsyncIterator<TResult> Limit<TResult>(
			IAsyncEnumerable<TResult> source,
			Func<TResult, bool> condition)
		{
			return new TakeWhileAsyncIterator<TResult>(source, condition);
		}

		#endregion

		#region Run...

		/// <summary>Immediately execute an action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">If different than default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			[InstantHandle] Action<TSource> action,
			CancellationToken ct)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			ct.ThrowIfCancellationRequested();

			long count = 0;
			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					action(iterator.Current);
					++count;
				}
			}
			return count;
		}

		/// <summary>Immediately execute an action on each element of an async sequence, with the possibility of stopping before the end</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">If different than default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Lambda called for each element as it arrives. If the return value is true, the next value will be processed. If the return value is false, the iterations will stop immediately.</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed successfully</returns>
		internal static async Task<long> Run<TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			Func<TSource, bool> action,
			CancellationToken ct)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			ct.ThrowIfCancellationRequested();

			long count = 0;
			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (!action(iterator.Current))
					{
						break;
					}
					++count;
				}
			}
			return count;
		}

		/// <summary>Immediately execute an async action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">Expected execution mode of the query</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			Func<TSource, CancellationToken, Task> action,
			CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long count = 0;
			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					await action(iterator.Current, ct).ConfigureAwait(false);
					++count;
				}
			}
			return count;
		}

		/// <summary>Immediately execute an async action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">Expected execution mode of the query</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			Func<TSource, Task> action,
			CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long count = 0;
			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					ct.ThrowIfCancellationRequested();
					await action(iterator.Current).ConfigureAwait(false);
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
		public static async Task<TSource> Head<TSource>(
			IAsyncEnumerable<TSource> source,
			bool single,
			bool orDefault,
			CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, AsyncIterationHint.Head) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				if (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					TSource first = iterator.Current;
					if (single)
					{
						if (await iterator.MoveNextAsync().ConfigureAwait(false)) throw new InvalidOperationException("The sequence contained more than one element");
					}
					return first;
				}
				if (!orDefault) throw new InvalidOperationException("The sequence was empty");
				return default!;
			}
		}

		#endregion

	}

	/// <summary>Small buffer that keeps a list of chunks that are larger and larger</summary>
	/// <typeparam name="T">Type of elements stored in the buffer</typeparam>
	[DebuggerDisplay("Count={Count}, Chunks={this.Chunks.Length}, Current={Index}/{Current.Length}")]
	public sealed class Buffer<T>
	{
		// We want to avoid growing the same array again and again !
		// Instead, we grow list of chunks, that grow in size (until a max), and concatenate all the chunks together at the end, once we know the final size

		/// <summary>Default initial capacity, if not specified</summary>
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
			// - except the first chunk who is set to the initial capacity

			Array.Resize(ref this.Chunks, this.Chunks.Length + 1);
			this.Current = new T[Math.Min(this.Count, MaxChunkSize)];
			this.Chunks[this.Chunks.Length - 1] = this.Current;
			this.Index = 0;
		}

		private T[] MergeChunks()
		{
			var tmp = new T[this.Count];
			int count = this.Count;
			int index = 0;
			for (int i = 0; i < this.Chunks.Length - 1; i++)
			{
				var chunk = this.Chunks[i];
				Array.Copy(chunk, 0, tmp, index, chunk.Length);
				index += chunk.Length;
				count -= chunk.Length;
			}
			Array.Copy(this.Current, 0, tmp, index, count);
			return tmp;
		}

		/// <summary>Return a buffer containing all of the items</summary>
		/// <returns>Buffer that contains all the items, and may be larger than required</returns>
		/// <remarks>This is equivalent to calling ToArray(), except that if the buffer is empty, or if it consists of a single page, then no new allocations will be performed.</remarks>
		public T[] GetBuffer()
		{
			//note: this is called by internal operator like OrderBy
			// In this case we want to reduce the copying as much as possible,
			// and we can suppose that the buffer won't be exposed to the application

			if (this.Count == 0)
			{ // empty
				return Array.Empty<T>();
			}
			else if (this.Chunks.Length == 1)
			{ // everything fits in a single chunk
				return this.Current;
			}
			else
			{ // we need to stitch all the buffers together
				return MergeChunks();
			}
		}

		/// <summary>Return the content of the buffer</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		public T[] ToArray()
		{
			if (this.Count == 0)
			{ // empty sequence
				return Array.Empty<T>();
			}
			else if (this.Chunks.Length == 1 && this.Current.Length == this.Count)
			{ // a single buffer page was used
				return this.Current;
			}
			else
			{ // concatenate all the buffer pages into one big array
				return MergeChunks();
			}
		}

		/// <summary>Return the content of the buffer</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		public List<T> ToList()
		{
			int count = this.Count;
			if (count == 0)
			{ // empty sequence
				return new List<T>();
			}

			var list = new List<T>(count);
			if (count > 0)
			{
				var chunks = this.Chunks;
				for (int i = 0; i < chunks.Length - 1; i++)
				{
					list.AddRange(chunks[i]);
					count -= chunks[i].Length;
				}

				var current = this.Current;
				if (count == current.Length)
				{ // the last chunk fits perfectly
					list.AddRange(current);
				}
				else
				{ // there is no List<T>.AddRange(buffer, offset, count), and copying in a tmp buffer would waste the memory we tried to save with the buffer
				  // also, for most of the small queries, like FirstOrDefault()/SingleOrDefault(), count will be 1 (or very small) so calling Add(T) will still be optimum
					for (int i = 0; i < count; i++)
					{
						list.Add(current[i]);
					}
				}
			}

			return list;
		}

		/// <summary>Return the content of the buffer</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		public HashSet<T> ToHashSet(IEqualityComparer<T>? comparer = null)
		{
			int count = this.Count;
			var hashset = new HashSet<T>(comparer);
			if (count == 0)
			{
				return hashset;
			}

			var chunks = this.Chunks;

			for (int i = 0; i < chunks.Length - 1; i++)
			{
				foreach (var item in chunks[i])
				{
					hashset.Add(item);
				}
				count -= chunks[i].Length;
			}

			var current = this.Current;
			if (count == current.Length)
			{ // the last chunk fits perfectly
				foreach (var item in current)
				{
					hashset.Add(item);
				}
			}
			else
			{ // there is no List<T>.AddRange(buffer, offset, count), and copying in a tmp buffer would waste the memory we tried to save with the buffer
			  // also, for most of the small queries, like FirstOrDefault()/SingleOrDefault(), count will be 1 (or very small) so calling Add(T) will still be optimum
				for (int i = 0; i < count; i++)
				{
					hashset.Add(current[i]);
				}
			}
			return hashset;
		}

	}

}
