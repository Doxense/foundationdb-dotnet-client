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
		internal abstract class AsyncIterator<TResult> : IFdbAsyncEnumerable<TResult>, IFdbAsyncEnumerator<TResult>
		{
			private const int STATE_SEQ = 0;
			private const int STATE_INIT = 1;
			private const int STATE_ITERATING = 2;
			private const int STATE_COMPLETED = 3;
			private const int STATE_DISPOSED = -1;

			protected TResult m_current;
			protected int m_state;

			#region IFdbAsyncEnumerable<TResult>...

			public IFdbAsyncEnumerator<TResult> GetEnumerator()
			{
				// reuse the same instance the first time
				if (Interlocked.CompareExchange(ref m_state, STATE_INIT, STATE_SEQ) == STATE_SEQ)
				{
					return this;
				}
				// create a new one
				var iter = Clone();
				Volatile.Write(ref iter.m_state, STATE_INIT);
				return iter;
			}

			protected abstract AsyncIterator<TResult> Clone();

			#endregion

			#region IFdbAsyncEnumerator<TResult>...

			public TResult Current
			{
				get
				{
					if (Volatile.Read(ref m_state) != STATE_ITERATING) ThrowInvalidState();
					return m_current;
				}
			}

			public async Task<bool> MoveNext(CancellationToken ct)
			{
				var state = Volatile.Read(ref m_state);

				if (state == STATE_COMPLETED)
				{
					return false;
				}
				if (state != STATE_INIT && state != STATE_ITERATING)
				{
					ThrowInvalidState();
					return false;
				}

				if (ct.IsCancellationRequested)
				{
					return Cancelled(ct);
				}

				try
				{
					if (state == STATE_INIT)
					{
						if (!await OnFirstAsync(ct).ConfigureAwait(false))
						{ // did not start at all ?
							return Completed();
						}

						if (Interlocked.CompareExchange(ref m_state, STATE_ITERATING, STATE_INIT) != STATE_INIT)
						{ // something happened while we where starting ?
							return false;
						}
					}

					return await OnNextAsync(ct).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					Failed(e);
					throw;
				}
			}

			#endregion

			#region LINQ...

			public abstract AsyncIterator<TResult> Where(Func<TResult, bool> predicate);

			public abstract AsyncIterator<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate);

			public abstract AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector);

			public abstract AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector);

			public abstract AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector);

			public abstract AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector);

			public abstract AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector);

			public abstract AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TResult, TCollection, TNew> resultSelector);

			public abstract AsyncIterator<TResult> Take(int limit);

			public virtual Task ExecuteAsync(Action<TResult> action, CancellationToken ct)
			{
				return FdbAsyncEnumerable.Run<TResult>(this, action, ct);
			}

			public virtual Task ExecuteAsync(Func<TResult, CancellationToken, Task> asyncAction, CancellationToken ct)
			{
				return FdbAsyncEnumerable.Run<TResult>(this, asyncAction, ct);
			}

			#endregion

			#region Iterator Impl...

			protected abstract Task<bool> OnFirstAsync(CancellationToken ct);

			protected abstract Task<bool> OnNextAsync(CancellationToken ct);

			protected bool Publish(TResult current)
			{
				if (Volatile.Read(ref m_state) == STATE_ITERATING)
				{
					m_current = current;
					return true;
				}
				return false;
			}

			protected bool Completed()
			{
				if (m_state == STATE_INIT)
				{ // nothing should have been done by the iterator..
					Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_INIT);
				}
				else if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_ITERATING) == STATE_ITERATING)
				{ // the iterator has done at least something, so we can clean it up
					this.Cleanup();
				}
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
				switch(m_state)
				{
					case STATE_SEQ:
						throw new InvalidOperationException("The async iterator should have been initiliazed with a called to GetEnumerator()");

					case STATE_ITERATING:
						break;
					
					case STATE_DISPOSED:
						throw new ObjectDisposedException(null, "The async iterator has already been closed");

					default:
						throw new InvalidOperationException();
				}
				
			}

			protected abstract void Cleanup();

			#endregion

			#region IDisposable...

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
						Cleanup();
					}
					finally
					{
						m_current = default(TResult);
					}
				}
			}

			#endregion
		}

		internal abstract class AsyncFilter<TSource, TResult> : AsyncIterator<TResult>
		{
			/// <summary>Source sequence (when in iterable mode)</summary>
			protected IFdbAsyncEnumerable<TSource> m_source;

			/// <summary>Active iterator on the source (when in terator mode)</summary>
			protected IFdbAsyncEnumerator<TSource> m_iterator;

			protected AsyncFilter(IFdbAsyncEnumerable<TSource> source)
			{
				Contract.Requires(source!= null);
				m_source = source;
			}

			protected override Task<bool> OnFirstAsync(CancellationToken ct)
			{
				// on the first call to MoveNext, we have to hook up with the source iterator

				IFdbAsyncEnumerator<TSource> iterator = null;
				try
				{
					iterator = m_source.GetEnumerator();
					return TaskHelpers.FromResult(iterator != null);
				}
				catch(Exception)
				{
					// whatever happens, make sure that we released the iterator...
					if (iterator != null)
					{
						iterator.Dispose();
						iterator = null;
					}
					throw;
				}
				finally
				{
					m_iterator = iterator;
				}
			}

			protected override void Cleanup()
			{
				var iterator = Interlocked.Exchange(ref m_iterator, null);
				if (iterator != null)
				{
					iterator.Dispose();
				}
			}

			public override AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TResult, TCollection, TNew> resultSelector)
			{
				if (asyncCollectionSelector == null) throw new ArgumentNullException("asyncCollectionSelector");
				if (resultSelector == null) throw new ArgumentNullException("resultSelector");

				return new SelectManyAsyncIterator<TResult, TCollection, TNew>(this, null, asyncCollectionSelector, resultSelector);
			}

			public override AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
			{
				if (collectionSelector == null) throw new ArgumentNullException("collectionSelector");
				if (resultSelector == null) throw new ArgumentNullException("resultSelector");

				return new SelectManyAsyncIterator<TResult, TCollection, TNew>(this, collectionSelector, null, resultSelector);
			}

			public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
			{
				return FdbAsyncEnumerable.Flatten(this, selector);
			}

			public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
			{
				return FdbAsyncEnumerable.Flatten(this, asyncSelector);
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
			{
				return FdbAsyncEnumerable.Map(this, selector);
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
			{
				return FdbAsyncEnumerable.Map(this, asyncSelector);
			}

			public override AsyncIterator<TResult> Where(Func<TResult, bool> predicate)
			{
				return FdbAsyncEnumerable.Filter(this, predicate);
			}

			public override AsyncIterator<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
			{
				return FdbAsyncEnumerable.Filter(this, asyncPredicate);
			}

			public override AsyncIterator<TResult> Take(int limit)
			{
				return FdbAsyncEnumerable.Limit(this, limit);
			}

		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class WhereSelectAsyncIterator<TSource, TResult> : AsyncFilter<TSource, TResult>
		{
			private Func<TSource, bool> m_filter;
			private Func<TSource, CancellationToken, Task<bool>> m_asyncFilter;

			private Func<TSource, TResult> m_transform;
			private Func<TSource, CancellationToken, Task<TResult>> m_asyncTransform;

			private int? m_limit;

			private int? m_remaining;

			public WhereSelectAsyncIterator(
				IFdbAsyncEnumerable<TSource> source,
				Func<TSource, bool> filter,
				Func<TSource, CancellationToken, Task<bool>> asyncFilter,
				Func<TSource, TResult> transform,
				Func<TSource, CancellationToken, Task<TResult>> asyncTransform,
				int? limit
			)
				: base(source)
			{
				Contract.Requires(transform != null ^ asyncTransform != null); // at least one but not both
				Contract.Requires(filter == null || asyncFilter == null); // can have none, but not both
				Contract.Requires(limit == null || limit >= 0);

				m_filter = filter;
				m_asyncFilter = asyncFilter;
				m_transform = transform;
				m_asyncTransform = asyncTransform;
				m_limit = limit;
			}


			protected override AsyncIterator<TResult> Clone()
			{
				return new WhereSelectAsyncIterator<TSource, TResult>(m_source, m_filter, m_asyncFilter, m_transform, m_asyncTransform, m_limit);
			}

			protected override Task<bool> OnFirstAsync(CancellationToken ct)
			{
				m_remaining = m_limit;
				return base.OnFirstAsync(ct);
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				if (m_remaining != null && m_remaining.Value <= 0)
				{ // reached limit!
					return Completed();
				}

				while (!cancellationToken.IsCancellationRequested)
				{
					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					TSource current = m_iterator.Current;
					if (m_filter != null)
					{
						if (!m_filter(current)) continue;
					}
					else if (m_asyncFilter != null)
					{
						if (!await m_asyncFilter(current, cancellationToken).ConfigureAwait(false)) continue;
					}

					TResult result;
					if (m_transform != null)
					{
						result = m_transform(current);
					}
					else
					{
						result = await m_asyncTransform(current, cancellationToken).ConfigureAwait(false);
					}

					if (m_remaining != null)
					{ // decrement remaining quota
						m_remaining = m_remaining.Value - 1;
					}

					return Publish(result);
				}

				return Cancelled(cancellationToken);
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
			{
				if (selector == null) throw new ArgumentNullException("selector");

				if (m_transform != null)
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						(x) => selector(m_transform(x)),
						null,
						m_limit
					);
				}
				else
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						null,
						async (x, ct) => selector(await m_asyncTransform(x, ct).ConfigureAwait(false)),
						m_limit
					);
				}
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
			{
				if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

				if (m_transform != null)
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						null,
						(x, ct) => asyncSelector(m_transform(x), ct),
						m_limit
					);
				}
				else
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						null,
						async (x, ct) => await asyncSelector(await m_asyncTransform(x, ct).ConfigureAwait(false), ct).ConfigureAwait(false),
						m_limit
					);
				}
			}

			public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
			{
				if (selector == null) throw new ArgumentNullException("selector");

				if (m_filter == null && m_asyncFilter == null && m_limit == null)
				{
					if (m_transform != null)
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: (x) => selector(m_transform(x)),
							asyncSelector: null
						);
					}
					else
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: null,
							asyncSelector: async (x, ct) => selector(await m_asyncTransform(x, ct).ConfigureAwait(false))
						);
					}
				}

				// other cases are too complex :(
				return base.SelectMany<TNew>(selector);
			}
			public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
			{
				if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

				if (m_filter == null && m_asyncFilter == null && m_limit == null)
				{
					if (m_transform != null)
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: null,
							asyncSelector: (x, ct) => asyncSelector(m_transform(x), ct)
						);
					}
					else
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: null,
							asyncSelector: async (x, ct) => await asyncSelector(await m_asyncTransform(x, ct).ConfigureAwait(false), ct).ConfigureAwait(false)
						);
					}
				}

				// other cases are too complex :(
				return base.SelectMany<TNew>(asyncSelector);
			}

			public override AsyncIterator<TResult> Take(int limit)
			{
				if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Limit cannot be less than zero");

				if (m_limit != null && m_limit.Value <= limit)
				{
					// we are already taking less then that
					return this;
				}

				return new WhereSelectAsyncIterator<TSource, TResult>(
					m_source,
					m_filter,
					m_asyncFilter,
					m_transform,
					m_asyncTransform,
					m_limit
				);
			}

		}

		/// <summary>Filters an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		internal sealed class WhereAsyncIterator<TSource> : AsyncFilter<TSource, TSource>
		{
			private Func<TSource, bool> m_filter;
			private Func<TSource, CancellationToken, Task<bool>> m_asyncFilter;

			public WhereAsyncIterator(IFdbAsyncEnumerable<TSource> source, Func<TSource, bool> filter, Func<TSource, CancellationToken, Task<bool>> asyncFilter)
				: base(source)
			{
				Contract.Requires(filter != null ^ asyncFilter != null);

				m_filter = filter;
				m_asyncFilter = asyncFilter;
			}

			protected override AsyncIterator<TSource> Clone()
			{
				return new WhereAsyncIterator<TSource>(m_source, m_filter, m_asyncFilter);
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
					if (m_filter != null)
					{
						if (!m_filter(current))
						{
							continue;
						}
					}
					else
					{
						if (!await m_asyncFilter(current, cancellationToken).ConfigureAwait(false))
						{
							continue;
						}
					}

					return Publish(current);
				}

				return Cancelled(cancellationToken);
			}

			public override AsyncIterator<TSource> Where(Func<TSource, bool> predicate)
			{
				return FdbAsyncEnumerable.Filter<TSource>(
					m_source,
					async (x, ct) => (await m_asyncFilter(x, ct)) && predicate(x)
				);
			}

			public override AsyncIterator<TSource> Where(Func<TSource, CancellationToken, Task<bool>> asyncPredicate)
			{
				return FdbAsyncEnumerable.Filter<TSource>(
					m_source,
					async (x, ct) => (await m_asyncFilter(x, ct)) && (await asyncPredicate(x, ct))
				);
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TSource, TNew> selector)
			{
				return new WhereSelectAsyncIterator<TSource, TNew>(
					m_source,
					m_filter,
					m_asyncFilter,
					selector,
					null,
					null
				);
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TSource, CancellationToken, Task<TNew>> asyncSelector)
			{
				return new WhereSelectAsyncIterator<TSource, TNew>(
					m_source,
					m_filter,
					m_asyncFilter,
					null,
					asyncSelector,
					null
				);
			}

			public override AsyncIterator<TSource> Take(int limit)
			{
				if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Limit cannot be less than zero");

				return new WhereSelectAsyncIterator<TSource, TSource>(
					m_source,
					m_filter,
					m_asyncFilter,
					TaskHelpers.Cache<TSource>.Identity,
					null,
					limit
				);
			}

			public override async Task ExecuteAsync(Action<TSource> handler, CancellationToken ct)
			{
				if (handler == null) throw new ArgumentNullException("handler");

				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				using (var iter = m_source.GetEnumerator())
				{
					if (m_filter != null)
					{
						while (!ct.IsCancellationRequested && (await iter.MoveNext(ct).ConfigureAwait(false)))
						{
							var current = iter.Current;
							if (m_filter(current))
							{
								handler(current);
							}
						}
					}
					else
					{
						while(!ct.IsCancellationRequested && (await iter.MoveNext(ct).ConfigureAwait(false)))
						{
							var current = iter.Current;
							if (await m_asyncFilter(current, ct).ConfigureAwait(false))
							{
								handler(current);
							}
						}
					}
				}

				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			}

			public override async Task ExecuteAsync(Func<TSource, CancellationToken, Task> asyncHandler, CancellationToken ct)
			{
				if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");

				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				using (var iter = m_source.GetEnumerator())
				{
					if (m_filter != null)
					{
						while (!ct.IsCancellationRequested && (await iter.MoveNext(ct).ConfigureAwait(false)))
						{
							var current = iter.Current;
							if (m_filter(current))
							{
								await asyncHandler(current, ct).ConfigureAwait(false);
							}
						}
					}
					else
					{
						while (!ct.IsCancellationRequested && (await iter.MoveNext(ct).ConfigureAwait(false)))
						{
							var current = iter.Current;
							if (await m_asyncFilter(current, ct).ConfigureAwait(false))
							{
								await asyncHandler(current, ct).ConfigureAwait(false);
							}
						}
					}
				}

				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
			}

		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class SelectManyAsyncIterator<TSource, TResult> : AsyncFilter<TSource, TResult>
		{
			private Func<TSource, IEnumerable<TResult>> m_selector;
			private Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> m_asyncSelector;
			private IEnumerator<TResult> m_batch;

			public SelectManyAsyncIterator(IFdbAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
				: base(source)
			{
				// Must have at least one, but not both
				Contract.Requires(selector != null ^ asyncSelector != null);

				m_selector = selector;
				m_asyncSelector = asyncSelector;
			}

			protected override AsyncIterator<TResult> Clone()
			{
				return new SelectManyAsyncIterator<TSource, TResult>(m_source, m_selector, m_asyncSelector);
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

						IEnumerable<TResult> sequence;
						if (m_selector != null)
						{
							sequence = m_selector(m_iterator.Current);
						}
						else
						{
							sequence = await m_asyncSelector(m_iterator.Current, cancellationToken).ConfigureAwait(false);
						}
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

			protected override void Cleanup()
			{
				try
				{
					if (m_batch != null)
					{
						m_batch.Dispose();
					}
				}
				finally
				{
					m_batch = null;
					base.Cleanup();
				}
			}
		}

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class SelectManyAsyncIterator<TSource, TCollection, TResult> : AsyncFilter<TSource, TResult>
		{
			private Func<TSource, IEnumerable<TCollection>> m_collectionSelector;
			private Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> m_asyncCollectionSelector;
			private Func<TSource, TCollection, TResult> m_resultSelector;
			private TSource m_sourceCurrent;
			private IEnumerator<TCollection> m_batch;

			public SelectManyAsyncIterator(
				IFdbAsyncEnumerable<TSource> source,
				Func<TSource, IEnumerable<TCollection>> collectionSelector,
				Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector,
				Func<TSource, TCollection, TResult> resultSelector
			)
				: base(source)
			{
				// must have at least one but not both
				Contract.Requires(collectionSelector != null ^ asyncCollectionSelector != null);
				Contract.Requires(resultSelector != null);

				m_collectionSelector = collectionSelector;
				m_asyncCollectionSelector = asyncCollectionSelector;
				m_resultSelector = resultSelector;
			}

			protected override AsyncIterator<TResult> Clone()
			{
				return new SelectManyAsyncIterator<TSource, TCollection, TResult>(m_source, m_collectionSelector, m_asyncCollectionSelector, m_resultSelector);
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

						IEnumerable<TCollection> sequence;

						if (m_collectionSelector != null)
						{
							sequence = m_collectionSelector(m_sourceCurrent);
						}
						else
						{
							sequence = await m_asyncCollectionSelector(m_sourceCurrent, cancellationToken).ConfigureAwait(false);
						}
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

			protected override void Cleanup()
			{
				try
				{
					if (m_batch != null)
					{
						m_batch.Dispose();
					}
				}
				finally
				{
					m_batch = null;
					base.Cleanup();
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
