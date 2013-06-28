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
using System.Runtime.ExceptionServices;
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
		private sealed class EmptySequence<TSource> : IFdbAsyncEnumerable<TSource>, IFdbAsyncEnumerator<TSource>
		{
			public static readonly EmptySequence<TSource> Default = new EmptySequence<TSource>();

			private EmptySequence()
			{ }

			Task<bool> IFdbAsyncEnumerator<TSource>.MoveNext(CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested();
				return FalseTask;
			}

			TSource IFdbAsyncEnumerator<TSource>.Current
			{
				get { throw new InvalidOperationException("This sequence is emty"); }
			}

			void IDisposable.Dispose()
			{
				// NOOP!
			}

			public IFdbAsyncEnumerator<TSource> GetEnumerator()
			{
				return this;
			}
		}

		/// <summary>Wraps an async sequence of items into another async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class AsyncSequence<TSource, TResult> : IFdbAsyncEnumerable<TResult>
		{
			public readonly IFdbAsyncEnumerable<TSource> Source;
			public readonly Func<IFdbAsyncEnumerator<TSource>, IFdbAsyncEnumerator<TResult>> Factory;

			public AsyncSequence(IFdbAsyncEnumerable<TSource> source, Func<IFdbAsyncEnumerator<TSource>, IFdbAsyncEnumerator<TResult>> factory)
			{
				this.Source = source;
				this.Factory = factory;
			}

			public IFdbAsyncEnumerator<TResult> GetEnumerator()
			{
				IFdbAsyncEnumerator<TSource> inner = null;
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

		/// <summary>Base class for all async iterators</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal abstract class AsyncIterator<TSource, TResult> : IFdbAsyncEnumerator<TResult>
		{
			private const int STATE_ALIVE = 0;
			private const int STATE_DISPOSED = -1;

			protected IFdbAsyncEnumerator<TSource> m_iterator;
			protected TResult m_current;
			protected int m_state;

			protected AsyncIterator(IFdbAsyncEnumerator<TSource> iterator)
			{
				Contract.Requires(iterator != null);
				m_iterator = iterator;
			}

			public TResult Current
			{
				get
				{
					if (m_state == STATE_DISPOSED) ThrowInvalidState();
					return m_current;
				}
			}

			public async Task<bool> MoveNext(CancellationToken ct)
			{
				switch (m_state)
				{
					case STATE_ALIVE:
					{
						if (ct.IsCancellationRequested)
						{
							return Cancelled(ct);
						}

						try
						{
							return await OnNextAsync(ct).ConfigureAwait(false);
						}
						catch (Exception e)
						{
							Failed(e);
							throw;
						}
					}
					default:
					{
						ThrowInvalidState();
						return false;
					}
				}
			}

			protected abstract Task<bool> OnNextAsync(CancellationToken ct);

			protected bool Publish(TResult current)
			{
				if (m_state == STATE_ALIVE)
				{
					m_current = current;
					return true;
				}
				return false;
			}

			protected bool Completed()
			{
				this.Dispose();
				return false;
			}

			protected bool Failed(Exception e)
			{
				this.Dispose();
				return false;
			}

			protected bool Cancelled(CancellationToken cancellationToken)
			{
				this.Dispose();
				cancellationToken.ThrowIfCancellationRequested();
				return false;
			}

			protected void ThrowInvalidState()
			{
				if (m_state != STATE_ALIVE) throw new InvalidOperationException();
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing)
			{
				if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
				{
					try
					{
						if (disposing && m_iterator != null)
						{
							m_iterator.Dispose();
						}
					}
					finally
					{
						m_iterator = null;
						m_current = default(TResult);
					}
				}
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class WhereSelectorIterator<TSource, TResult> : AsyncIterator<TSource, TResult>
		{
			private Func<TSource, bool> m_filter;
			private Func<TSource, TResult> m_transform;

			public WhereSelectorIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, bool> filter, Func<TSource, TResult> transform)
				: base(iterator)
			{
				Contract.Requires(filter != null || transform != null);

				m_filter = filter;
				m_transform = transform;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					var current = m_iterator.Current;
					if (m_filter == null || m_filter(current))
					{
						return Publish(m_transform(current));
					}
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					m_filter = null;
					m_transform = null;
				}
				finally
				{
					base.Dispose(disposing);
				}
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class WhereSelectAsyncIterator<TSource, TResult> : AsyncIterator<TSource, TResult>
		{
			private Func<TSource, bool> m_filter;
			private Func<TSource, CancellationToken, Task<TResult>> m_transform;

			public WhereSelectAsyncIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, bool> filter, Func<TSource, CancellationToken, Task<TResult>> transform)
				: base(iterator)
			{
				Contract.Requires(transform != null);

				m_filter = filter;
				m_transform = transform;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					TSource current = m_iterator.Current;
					if (m_filter == null || m_filter(current))
					{
						TResult result = await m_transform(current, cancellationToken).ConfigureAwait(false);
						return Publish(result);
					}
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					m_filter = null;
					m_transform = null;
				}
				finally
				{
					base.Dispose(disposing);
				}
			}
		}

		/// <summary>Filters an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		internal sealed class WhereIterator<TSource> : AsyncIterator<TSource, TSource>
		{
			private Func<TSource, bool> m_filter;

			public WhereIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, bool> filter)
				: base(iterator)
			{
				Contract.Requires(filter != null);

				m_filter = filter;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{

				while (!cancellationToken.IsCancellationRequested)
				{
					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					TSource current = m_iterator.Current;
					if (!m_filter(current))
					{
						continue;
					}

					return Publish(current);
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					m_filter = null;
				}
				finally
				{
					base.Dispose(disposing);
				}
			}
		}

		/// <summary>Filters an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		internal sealed class WhereAsyncIterator<TSource> : AsyncIterator<TSource, TSource>
		{
			private Func<TSource, CancellationToken, Task<bool>> m_filter;

			public WhereAsyncIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, CancellationToken, Task<bool>> asyncFilter)
				: base(iterator)
			{
				Contract.Requires(iterator != null && asyncFilter != null);

				m_iterator = iterator;
				m_filter = asyncFilter;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{

				while (!cancellationToken.IsCancellationRequested)
				{
					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					TSource current = m_iterator.Current;
					if (!await m_filter(current, cancellationToken).ConfigureAwait(false))
					{
						continue;
					}

					return Publish(current);
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					m_filter = null;
				}
				finally
				{
					base.Dispose(disposing);
				}
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class SelectManyIterator<TSource, TResult> : AsyncIterator<TSource, TResult>
		{
			private Func<TSource, IEnumerable<TResult>> m_transform;
			private IEnumerator<TResult> m_batch;

			public SelectManyIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, IEnumerable<TResult>> selector)
				: base(iterator)
			{
				m_transform = selector;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				// if we are in a batch, iterate over it
				// if not, wait for the next batch

				while (!cancellationToken.IsCancellationRequested)
				{

					if (m_batch == null)
					{

						if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
						{ // inner completed
							return Completed();
						}

						if (cancellationToken.IsCancellationRequested) break;

						var sequence = m_transform(m_iterator.Current);
						if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

						m_batch = sequence.GetEnumerator();
						Contract.Assert(m_batch != null);
					}

					if (!m_batch.MoveNext())
					{ // the current batch is exhausted, move to the next
						m_batch.Dispose();
						m_batch = null;
						continue;
					}

					return Publish(m_batch.Current);
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					if (disposing && m_batch != null)
					{
						m_batch.Dispose();
					}
				}
				finally
				{
					m_batch = null;
					m_transform = null;
				}
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class SelectManyAsyncIterator<TSource, TResult> : AsyncIterator<TSource, TResult>
		{
			private Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> m_transform;
			private IEnumerator<TResult> m_batch;

			public SelectManyAsyncIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
				: base(iterator)
			{
				m_transform = asyncSelector;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				// if we are in a batch, iterate over it
				// if not, wait for the next batch

				while (!cancellationToken.IsCancellationRequested)
				{

					if (m_batch == null)
					{

						if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
						{ // inner completed
							return Completed();
						}

						if (cancellationToken.IsCancellationRequested) break;

						var sequence = await m_transform(m_iterator.Current, cancellationToken).ConfigureAwait(false);
						if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

						m_batch = sequence.GetEnumerator();
						Contract.Assert(m_batch != null);
					}

					if (!m_batch.MoveNext())
					{ // the current batch is exhausted, move to the next
						m_batch.Dispose();
						m_batch = null;
						continue;
					}

					return Publish(m_batch.Current);
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					if (disposing && m_batch != null)
					{
						m_batch.Dispose();
					}
				}
				finally
				{
					m_batch = null;
					m_transform = null;
				}
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class SelectManyIterator<TSource, TCollection, TResult> : AsyncIterator<TSource, TResult>
		{
			private Func<TSource, IEnumerable<TCollection>> m_collectionSelector;
			private Func<TSource, TCollection, TResult> m_resultSelector;
			private TSource m_sourceCurrent;
			private IEnumerator<TCollection> m_batch;

			public SelectManyIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
				: base(iterator)
			{
				m_collectionSelector = collectionSelector;
				m_resultSelector = resultSelector;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				// if we are in a batch, iterate over it
				// if not, wait for the next batch

				while (!cancellationToken.IsCancellationRequested)
				{

					if (m_batch == null)
					{

						if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
						{ // inner completed
							return Completed();
						}

						if (cancellationToken.IsCancellationRequested) break;

						m_sourceCurrent = m_iterator.Current;

						var sequence = m_collectionSelector(m_sourceCurrent);
						if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

						m_batch = sequence.GetEnumerator();
						Contract.Assert(m_batch != null);
					}

					if (!m_batch.MoveNext())
					{ // the current batch is exhausted, move to the next
						m_batch.Dispose();
						m_batch = null;
						m_sourceCurrent = default(TSource);
						continue;
					}

					return Publish(m_resultSelector(m_sourceCurrent, m_batch.Current));
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					if (disposing && m_batch != null)
					{
						m_batch.Dispose();
					}
				}
				finally
				{
					m_batch = null;
					m_collectionSelector = null;
					m_resultSelector = null;
					m_sourceCurrent = default(TSource);
				}
			}
		
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class SelectManyAsyncIterator<TSource, TCollection, TResult> : AsyncIterator<TSource, TResult>
		{
			private Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> m_collectionSelector;
			private Func<TSource, TCollection, TResult> m_resultSelector;
			private TSource m_sourceCurrent;
			private IEnumerator<TCollection> m_batch;

			public SelectManyAsyncIterator(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TSource, TCollection, TResult> resultSelector)
				: base(iterator)
			{
				m_collectionSelector = asyncCollectionSelector;
				m_resultSelector = resultSelector;
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				// if we are in a batch, iterate over it
				// if not, wait for the next batch

				while (!cancellationToken.IsCancellationRequested)
				{

					if (m_batch == null)
					{

						if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
						{ // inner completed
							return Completed();
						}

						if (cancellationToken.IsCancellationRequested) break;

						m_sourceCurrent = m_iterator.Current;

						var sequence = await m_collectionSelector(m_sourceCurrent, cancellationToken).ConfigureAwait(false);
						if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

						m_batch = sequence.GetEnumerator();
						Contract.Requires(m_batch != null);
					}

					if (!m_batch.MoveNext())
					{ // the current batch is exhausted, move to the next
						m_batch.Dispose();
						m_batch = null;
						m_sourceCurrent = default(TSource);
						continue;
					}

					return Publish(m_resultSelector(m_sourceCurrent, m_batch.Current));
				}

				return Cancelled(cancellationToken);
			}

			protected override void Dispose(bool disposing)
			{
				try
				{
					if (disposing && m_batch != null)
					{
						m_batch.Dispose();
					}
				}
				finally
				{
					m_batch = null;
					m_collectionSelector = null;
					m_resultSelector = null;
					m_sourceCurrent = default(TSource);
				}
			}

		}

		#region IEnumerable<T> Adapters...

		/// <summary>Wraps a sequence of items into an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class EnumerableSequence<TSource, TResult> : IFdbAsyncEnumerable<TResult>
		{
			public readonly IEnumerable<TSource> Source;
			public readonly Func<IEnumerator<TSource>, IFdbAsyncEnumerator<TResult>> Factory;

			public EnumerableSequence(IEnumerable<TSource> source, Func<IEnumerator<TSource>, IFdbAsyncEnumerator<TResult>> factory)
			{
				this.Source = source;
				this.Factory = factory;
			}

			public IFdbAsyncEnumerator<TResult> GetEnumerator()
			{
				IEnumerator<TSource> inner = null;
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
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class EnumerableIterator<TSource, TResult> : IFdbAsyncEnumerator<TResult>
		{

			private IEnumerator<TSource> m_iterator;
			private Func<TSource, Task<TResult>> m_transform;
			private bool m_disposed;
			private TResult m_current;

			public EnumerableIterator(IEnumerator<TSource> iterator, Func<TSource, Task<TResult>> transform)
			{
				Contract.Requires(iterator != null && transform != null);

				m_iterator = iterator;
				m_transform = transform;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				if (m_disposed)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

				if (m_iterator.MoveNext())
				{
					m_current = await m_transform(m_iterator.Current).ConfigureAwait(false);
					return true;
				}

				m_current = default(TResult);
				m_disposed = true;
				return false;
			}

			public TResult Current
			{
				get
				{
					if (m_disposed) throw new InvalidOperationException();
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
				m_disposed = true;
				m_current = default(TResult);
			}

		}

		#endregion

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

		#region Map...

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="transform">Lambda called when a new inner element arrives that returns a transform item</param>
		/// <param name="onComplete">Called when the inner iterator has completed</param>
		/// <returns>New async iterator</returns>
		internal static WhereSelectorIterator<TSource, TResult> Map<TSource, TResult>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, bool> filter, Func<TSource, TResult> transform)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (transform == null) throw new ArgumentNullException("transform");

			return new WhereSelectorIterator<TSource, TResult>(iterator, filter, transform);
		}

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="transform">Async lambda called when a new inner element arrives that returns a Task that will return the transform item</param>
		/// <param name="onComplete">Called when the inner iterator has completed (optionnal)</param>
		/// <returns>New async iterator</returns>
		internal static WhereSelectAsyncIterator<TSource, TResult> Map<TSource, TResult>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, bool> filter, Func<TSource, CancellationToken, Task<TResult>> transform)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (transform == null) throw new ArgumentNullException("transform");

			return new WhereSelectAsyncIterator<TSource, TResult>(iterator, filter, transform);
		}

		#endregion

		#region Flatten...

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="selector">Lambda called when a new inner element arrives that returns a transform item</param>
		/// <param name="onComplete">Called when the inner iterator has completed</param>
		/// <returns>New async iterator</returns>
		internal static SelectManyIterator<TSource, TResult> Flatten<TSource, TResult>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, IEnumerable<TResult>> selector)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (selector == null) throw new ArgumentNullException("selector");

			return new SelectManyIterator<TSource, TResult>(iterator, selector);
		}

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="selector">Lambda called when a new inner element arrives that returns a transform item</param>
		/// <param name="onComplete">Called when the inner iterator has completed</param>
		/// <returns>New async iterator</returns>
		internal static SelectManyAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return new SelectManyAsyncIterator<TSource, TResult>(iterator, asyncSelector);
		}

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
		/// <typeparam name="TCollection">The type of the intermediate elements collected by <paramref name="collectionSelector"/>.</typeparam>
		/// <typeparam name="TResult">The type of the elements of the resulting async sequence.</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="collectionSelector">A transform function to apply to each element of the input async sequence.</param>
		/// <param name="resultSelector">A transform function to apply to each element of the intermediate sequence.</param>
		/// <param name="onComplete">Called when the inner iterator has completed</param>
		/// <returns>New async iterator</returns>
		internal static SelectManyIterator<TSource, TCollection, TResult> Flatten<TSource, TCollection, TResult>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (collectionSelector == null) throw new ArgumentNullException("collectionSelector");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			return new SelectManyIterator<TSource, TCollection, TResult>(iterator, collectionSelector, resultSelector);
		}

		/// <summary>Create a new async iterator over an inner async iterator, that will transform each item as they arrive</summary>
		/// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
		/// <typeparam name="TCollection">The type of the intermediate elements collected by <paramref name="asyncCollectionSelector"/>.</typeparam>
		/// <typeparam name="TResult">The type of the elements of the resulting async sequence.</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="asyncCollectionSelector">A transform function to apply to each element of the input async sequence.</param>
		/// <param name="resultSelector">A transform function to apply to each element of the intermediate sequence.</param>
		/// <param name="onComplete">Called when the inner iterator has completed</param>
		/// <returns>New async iterator</returns>
		internal static SelectManyAsyncIterator<TSource, TCollection, TResult> Flatten<TSource, TCollection, TResult>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (asyncCollectionSelector == null) throw new ArgumentNullException("asyncCollectionSelector");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			return new SelectManyAsyncIterator<TSource, TCollection, TResult>(iterator, asyncCollectionSelector, resultSelector);
		}

		#endregion

		#region Filter...

		/// <summary>Create a new async iterator over an async iterator, that will filter items as they arrive</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="predicate">Predicate called on each element. If true the element will be published on the outer async sequence. Otherwise it will be discarded.</param>
		/// <param name="onComplete">Called when the inner iterator has completed (optionnal)</param>
		/// <returns>New async iterator</returns>
		internal static WhereIterator<TSource> Filter<TSource>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, bool> predicate)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (predicate == null) throw new ArgumentNullException("predicate");

			return new WhereIterator<TSource>(iterator, predicate);
		}

		/// <summary>Create a new async iterator over an async iterator, that will filter items as they arrive</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="iterator">Inner iterator to use</param>
		/// <param name="asyncPredicate">Predicate called on each element. If true the element will be published on the outer async sequence. Otherwise it will be discarded.</param>
		/// <param name="onComplete">Called when the inner iterator has completed (optionnal)</param>
		/// <returns>New async iterator</returns>
		internal static WhereAsyncIterator<TSource> Filter<TSource>(IFdbAsyncEnumerator<TSource> iterator, Func<TSource, CancellationToken, Task<bool>> asyncPredicate)
		{
			if (iterator == null) throw new ArgumentNullException("iterator");
			if (asyncPredicate == null) throw new ArgumentNullException("asyncPredicate");

			return new WhereAsyncIterator<TSource>(iterator, asyncPredicate);
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

		/// <summary>Wraps a classic lambda into one that supports cancellation</summary>
		/// <param name="lambda">Lambda that does not support cancellation</param>
		/// <returns>New lambda that will check if the token is cancelled before calling <paramref name="lambda"/></returns>
		internal static Func<TSource, CancellationToken, TResult> WithCancellation<TSource, TResult>(Func<TSource, TResult> lambda)
		{
			Contract.Requires(lambda != null);
			return (value, ct) =>
			{
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
				return lambda(value);
			};
		}

	}
}
