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
		// Welcome to the wonderful world of the Monads! 

		/// <summary>Cached task that returns false</summary>
		internal static readonly Task<bool> FalseTask = Task.FromResult(false);

		/// <summary>Cached task that returns true</summary>
		internal static readonly Task<bool> TrueTask = Task.FromResult(true);

		/// <summary>An empty sequence</summary>
		private sealed class EmptySequence<T> : IFdbAsyncEnumerable<T>, IFdbAsyncEnumerator<T>
		{
			public static readonly EmptySequence<T> Default = new EmptySequence<T>();

			private EmptySequence()
			{ }

			Task<bool> IFdbAsyncEnumerator<T>.MoveNext(CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested();
				return FalseTask;
			}

			T IFdbAsyncEnumerator<T>.Current
			{
				get { throw new InvalidOperationException("This sequence is emty"); }
			}

			void IDisposable.Dispose()
			{
				// NOOP!
			}

			public IFdbAsyncEnumerator<T> GetEnumerator()
			{
				return this;
			}
		}

		/// <summary>Wraps an async sequence of items into another async sequence of items</summary>
		/// <typeparam name="T">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		internal sealed class AnonymousAsyncSelectable<T, R> : IFdbAsyncEnumerable<R>
		{
			public readonly IFdbAsyncEnumerable<T> Source;
			public readonly Func<IFdbAsyncEnumerator<T>, IFdbAsyncEnumerator<R>> Factory;

			public AnonymousAsyncSelectable(IFdbAsyncEnumerable<T> source, Func<IFdbAsyncEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
			{
				this.Source = source;
				this.Factory = factory;
			}

			public IFdbAsyncEnumerator<R> GetEnumerator()
			{
				IFdbAsyncEnumerator<T> inner = null;
				try
				{
					inner = this.Source.GetEnumerator();
					if (inner == null) throw new InvalidOperationException("The underlying async sequence returned an empty enumerator");

					var outer = this.Factory(inner);
					if (outer == null) throw new InvalidOperationException("The async factory returned en empty enumerator");

					return outer;
				}
				catch(Exception)
				{
					//make sure that the inner iterator gets disposed if something went wrong
					if (inner != null) inner.Dispose();
					throw;
				}
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="T">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		internal sealed class AnonymousAsyncSelector<T, R> : IFdbAsyncEnumerator<R>
		{
			private IFdbAsyncEnumerator<T> m_iterator;
			private Func<T, bool> m_preFilter;
			private Func<T, Task<R>> m_onNextAsync;
			private Func<T, R> m_onNextSync;
			private Func<R, bool> m_postFilter;
			private Func<Task> m_onComplete;
			private bool m_closed;
			private R m_current;

			public AnonymousAsyncSelector(IFdbAsyncEnumerator<T> iterator, Func<T, bool> preFilter, Func<T, R> onNext, Func<R, bool> postFilter, Func<Task> onComplete)
			{
				Contract.Requires(iterator != null && onNext != null);

				m_iterator = iterator;
				m_preFilter = preFilter;
				m_onNextSync = onNext;
				m_postFilter = postFilter;
				m_onComplete = onComplete;
			}

			public AnonymousAsyncSelector(IFdbAsyncEnumerator<T> iterator, Func<T, bool> preFilter, Func<T, Task<R>> onNextAsync, Func<R, bool> postFilter, Func<Task> onComplete)
			{
				Contract.Requires(iterator != null && onNextAsync != null);

				m_iterator = iterator;
				m_preFilter = preFilter;
				m_onNextAsync = onNextAsync;
				m_postFilter = postFilter;
				m_onComplete = onComplete;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				if (m_closed)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

				while (true)
				{
					if (!await m_iterator.MoveNext(cancellationToken))
					{ // completed
						break;
					}

					if (m_preFilter != null && !m_preFilter(m_iterator.Current))
					{ // filtered out
						continue;
					}

					R current;
					if (m_onNextAsync != null)
					{
						current = await m_onNextAsync(m_iterator.Current);
					}
					else
					{
						current = m_onNextSync(m_iterator.Current);
					}

					if (m_postFilter == null || m_postFilter(current))
					{
						m_current = current;
						return true;
					}
				}

				m_current = default(R);
				m_closed = true;
				if (m_onComplete != null)
				{
					await m_onComplete();
				}

				return false;
			}

			public R Current
			{
				get
				{
					if (m_closed) throw new InvalidOperationException();
					return m_current;
				}
			}

			public void Dispose()
			{
				if (m_iterator != null)
				{
					m_iterator.Dispose();
				}
				m_iterator = null;
				m_preFilter = null;
				m_onNextAsync = null;
				m_onNextSync = null;
				m_postFilter = null;
				m_onComplete = null;
				m_closed = true;
				m_current = default(R);
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="T">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		internal sealed class AnonymousAsyncManySelector<T, R> : IFdbAsyncEnumerator<R>
		{
			private IFdbAsyncEnumerator<T> m_iterator;
			private Func<T, bool> m_preFilter;
			private Func<T, Task<IEnumerable<R>>> m_onNextAsync;
			private Func<T, IEnumerable<R>> m_onNextSync;
			private Func<R, bool> m_postFilter;
			private Func<Task> m_onComplete;
			private bool m_closed;

			private T m_sourceCurrent;
			private IEnumerator<R> m_batch;
			private R m_current;

			public AnonymousAsyncManySelector(IFdbAsyncEnumerator<T> iterator, Func<T, bool> preFilter, Func<T, IEnumerable<R>> collectionSelector, Func<R, bool> postFilter, Func<Task> onComplete)
			{
				m_iterator = iterator;
				m_preFilter = preFilter;
				m_onNextSync = collectionSelector;
				m_postFilter = postFilter;
				m_onComplete = onComplete;
			}

			public AnonymousAsyncManySelector(IFdbAsyncEnumerator<T> iterator, Func<T, bool> preFilter, Func<T, Task<IEnumerable<R>>> asyncCollectionSelector, Func<R, bool> postFilter, Func<Task> onComplete)
			{
				m_iterator = iterator;
				m_preFilter = preFilter;
				m_onNextAsync = asyncCollectionSelector;
				m_postFilter = postFilter;
				m_onComplete = onComplete;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				if (m_closed)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

				// if we are in a batch, iterate over it
				// if not, wait for the next batch

				while (true)
				{

					if (m_batch == null)
					{

						if (!await m_iterator.MoveNext(cancellationToken))
						{ // inner completed
							break;
						}

						m_sourceCurrent = m_iterator.Current;

						if (m_preFilter != null && !m_preFilter(m_sourceCurrent))
						{ // filtered out
							continue;
						}

						IEnumerable<R> sequence;
						if (m_onNextAsync != null)
						{
							sequence = await m_onNextAsync(m_sourceCurrent);
						}
						else
						{
							sequence = m_onNextSync(m_sourceCurrent);
						}

						if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

						m_batch = sequence.GetEnumerator();
					}

					if (!m_batch.MoveNext())
					{
						m_batch.Dispose();
						m_batch = null;
						continue;
					}

					R batchCurrent = m_batch.Current;

					if (m_postFilter == null || m_postFilter(batchCurrent))
					{
						m_current = batchCurrent;
						return true;
					}
				}

				m_sourceCurrent = default(T);
				m_current = default(R);
				m_closed = true;
				if (m_onComplete != null)
				{
					await m_onComplete();
				}

				return false;
			}

			public R Current
			{
				get
				{
					if (m_closed) throw new InvalidOperationException();
					return m_current;
				}
			}

			public void Dispose()
			{
				if (m_batch != null)
				{
					m_batch.Dispose();
				}
				if (m_iterator != null)
				{
					m_iterator.Dispose();
				}
				m_iterator = null;
				m_batch = null;
				m_preFilter = null;
				m_onNextAsync = null;
				m_onNextSync = null;
				m_postFilter = null;
				m_onComplete = null;
				m_closed = true;
				m_sourceCurrent = default(T);
				m_current = default(R);
			}
		}

		/// <summary>Wraps a sequence of items into an async sequence of items</summary>
		/// <typeparam name="T">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		internal sealed class EnumerableSelectable<T, R> : IFdbAsyncEnumerable<R>
		{
			public readonly IEnumerable<T> Source;
			public readonly Func<IEnumerator<T>, IFdbAsyncEnumerator<R>> Factory;

			public EnumerableSelectable(IEnumerable<T> source, Func<IEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
			{
				this.Source = source;
				this.Factory = factory;
			}

			public IFdbAsyncEnumerator<R> GetEnumerator()
			{
				IEnumerator<T> inner = null;
				try
				{
					inner = this.Source.GetEnumerator();
					if (inner == null) throw new InvalidOperationException("The underlying sequence returned an empty enumerator");

					var outer = this.Factory(inner);
					if (outer == null) throw new InvalidOperationException("The async factory returned en empty enumerator");

					return outer;
				}
				catch (Exception)
				{
					//make sure that the inner iterator gets disposed if something went wrong
					if (inner != null) inner.Dispose();
					throw;
				}
			}
		}

		/// <summary>Iterates over a sequence of items</summary>
		/// <typeparam name="T">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		internal sealed class EnumerableSelector<T, R> : IFdbAsyncEnumerator<R>
		{

			private IEnumerator<T> m_iterator;
			private Func<T, Task<R>> m_transform;
			private bool m_closed;
			private R m_current;

			public EnumerableSelector(IEnumerator<T> iterator, Func<T, Task<R>> transform)
			{
				Contract.Requires(iterator != null && transform != null);

				m_iterator = iterator;
				m_transform = transform;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				if (m_closed)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

				if (m_iterator.MoveNext())
				{
					m_current = await m_transform(m_iterator.Current);
					return true;
				}

				m_current = default(R);
				m_closed = true;
				return false;
			}

			public R Current
			{
				get
				{
					if (m_closed) throw new InvalidOperationException();
					return m_current;
				}
			}

			public void Dispose()
			{
				if (m_iterator != null)
				{
					m_iterator.Dispose();
				}
				m_iterator = null;
				m_transform = null;
				m_closed = true;
				m_current = default(R);
			}

		}

		/// <summary>Create a new async sequence that will transform an inner async sequence</summary>
		/// <typeparam name="T">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source async sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static AnonymousAsyncSelectable<T, R> Create<T, R>(IFdbAsyncEnumerable<T> source, Func<IFdbAsyncEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
		{
			return new AnonymousAsyncSelectable<T, R>(source, factory);
		}

		/// <summary>Create a new async sequence that will transform an inner sequence</summary>
		/// <typeparam name="T">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static EnumerableSelectable<T, R> Create<T, R>(IEnumerable<T> source, Func<IEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
		{
			return new EnumerableSelectable<T, R>(source, factory);
		}

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="T">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="transform">Lambda called when a new inner element arrives that returns a transform item</param>
		/// <param name="onComplete">Called when the inner iterator has completed</param>
		/// <returns>New async iterator</returns>
		internal static AnonymousAsyncSelector<T, R> Iterate<T, R>(IFdbAsyncEnumerator<T> iterator, Func<T, R> transform, Func<Task> onComplete = null)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (transform == null) throw new ArgumentNullException("transform");

			return new AnonymousAsyncSelector<T, R>(iterator, preFilter: null, onNext: transform, postFilter: null, onComplete: onComplete);
		}

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="T">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="transform">Async lambda called when a new inner element arrives that returns a Task that will return the transform item</param>
		/// <param name="onComplete">Called when the inner iterator has completed (optionnal)</param>
		/// <returns>New async iterator</returns>
		internal static AnonymousAsyncSelector<T, R> Iterate<T, R>(IFdbAsyncEnumerator<T> iterator, Func<T, Task<R>> transform, Func<Task> onComplete = null)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (transform == null) throw new ArgumentNullException("transform");

			return new AnonymousAsyncSelector<T, R>(iterator, preFilter: null, onNextAsync: transform, postFilter: null, onComplete: onComplete);
		}

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="T">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="R">Type of elements of the outer async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="transform">Lambda called when a new inner element arrives that returns a transform item</param>
		/// <param name="onComplete">Called when the inner iterator has completed</param>
		/// <returns>New async iterator</returns>
		internal static AnonymousAsyncManySelector<T, R> IterateMany<T, R>(IFdbAsyncEnumerator<T> iterator, Func<T, IEnumerable<R>> transform, Func<Task> onComplete = null)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (transform == null) throw new ArgumentNullException("transform");

			return new AnonymousAsyncManySelector<T, R>(iterator, preFilter: null, collectionSelector: transform, postFilter: null, onComplete: onComplete);
		}

		/// <summary>Create a new async iterator over an async iterator, that will filter items as they arrive</summary>
		/// <typeparam name="T">Type of elements of the async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="predicate">Predicate called on each element. If true the element will be published on the outer async sequence. Otherwise it will be discarded.</param>
		/// <param name="onComplete">Called when the inner iterator has completed (optionnal)</param>
		/// <returns>New async iterator</returns>
		internal static AnonymousAsyncSelector<T, T> Filter<T>(IFdbAsyncEnumerator<T> iterator, Func<T, bool> predicate, Func<Task> onComplete = null)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (predicate == null) throw new ArgumentNullException("predicate");

			return new AnonymousAsyncSelector<T, T>(iterator, preFilter: predicate, onNext: (x) => x, postFilter: null, onComplete: onComplete);
		}

		/// <summary>Immediately execute an action on each element of an async sequence</summary>
		/// <typeparam name="T">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<T>(IFdbAsyncEnumerable<T> source, Action<T> action, CancellationToken ct)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			ct.ThrowIfCancellationRequested();

			long count = 0;
			using (var iterator = source.GetEnumerator())
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
		/// <typeparam name="T">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<long> Run<T>(IFdbAsyncEnumerable<T> source, Func<T, Task> action, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long count = 0;
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct))
				{
					await action(iterator.Current);
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
		internal static async Task<T> Head<T>(IFdbAsyncEnumerable<T> source, bool single, bool orDefault, CancellationToken ct)
		{
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The sequence returned a null async iterator");

				if (await iterator.MoveNext(ct))
				{
					T first = iterator.Current;
					if (single)
					{
						if (await iterator.MoveNext(ct)) throw new InvalidOperationException("The sequence contained more than one element");
					}
					return first;
				}
				if (!orDefault) throw new InvalidOperationException("The sequence was empty");
				return default(T);
			}
		}

	}
}
