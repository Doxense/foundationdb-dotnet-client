#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace Doxense.Linq.Async.Iterators
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq.Async.Expressions;
	using JetBrains.Annotations;

	/// <summary>Base class for all async iterators</summary>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	public abstract class AsyncIterator<TResult> : IAsyncEnumerable<TResult>, IAsyncEnumerator<TResult>
	{
		//REVIEW: we could need an IAsyncIterator<T> interface that holds all the Select(),Where(),Take(),... so that it can be used by AsyncEnumerable to either call them directly (if the query supports it) or use a generic implementation
		// => this would be implemented by AsyncIterator<T> as well as FdbRangeQuery<T> (and ony other 'self optimizing' class)

		private const int STATE_SEQ = 0;
		private const int STATE_INIT = 1;
		private const int STATE_ITERATING = 2;
		private const int STATE_COMPLETED = 3;
		private const int STATE_DISPOSED = -1;

		protected TResult m_current;
		protected int m_state;
		protected AsyncIterationHint m_mode;
		protected CancellationToken m_ct;

		#region IAsyncEnumerable<TResult>...

		public IAsyncEnumerator<TResult> GetEnumerator(CancellationToken ct, AsyncIterationHint mode)
		{
			ct.ThrowIfCancellationRequested();

			// reuse the same instance the first time
			if (Interlocked.CompareExchange(ref m_state, STATE_INIT, STATE_SEQ) == STATE_SEQ)
			{
				m_mode = mode;
				m_ct = ct;
				return this;
			}
			// create a new one
			var iter = Clone();
			iter.m_mode = mode;
			iter.m_ct = ct;
			Volatile.Write(ref iter.m_state, STATE_INIT);
			return iter;
		}

		protected abstract AsyncIterator<TResult> Clone();

		#endregion

		#region IAsyncEnumerator<TResult>...

		public TResult Current
		{
			get
			{
				if (Volatile.Read(ref m_state) != STATE_ITERATING) ThrowInvalidState();
				return m_current;
			}
		}

		public async Task<bool> MoveNextAsync()
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

			if (m_ct.IsCancellationRequested)
			{
				return Canceled();
			}

			try
			{
				if (state == STATE_INIT)
				{
					if (!await OnFirstAsync().ConfigureAwait(false))
					{ // did not start at all ?
						return Completed();
					}

					if (Interlocked.CompareExchange(ref m_state, STATE_ITERATING, STATE_INIT) != STATE_INIT)
					{ // something happened while we where starting ?
						return false;
					}
				}

				return await OnNextAsync().ConfigureAwait(false);
			}
			catch (Exception)
			{
				MarkAsFailed();
				throw;
			}
		}

		#endregion

		#region LINQ...

		[NotNull]
		public virtual AsyncIterator<TResult> Where([NotNull] Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate, nameof(predicate));

			return AsyncEnumerable.Filter<TResult>(this, new AsyncFilterExpression<TResult>(predicate));
		}

		[NotNull]
		public virtual AsyncIterator<TResult> Where([NotNull] Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			Contract.NotNull(asyncPredicate, nameof(asyncPredicate));

			return AsyncEnumerable.Filter<TResult>(this, new AsyncFilterExpression<TResult>(asyncPredicate));
		}

		[NotNull]
		public virtual AsyncIterator<TNew> Select<TNew>([NotNull] Func<TResult, TNew> selector)
		{
			Contract.NotNull(selector, nameof(selector));

			return AsyncEnumerable.Map<TResult, TNew>(this, new AsyncTransformExpression<TResult,TNew>(selector));
		}

		[NotNull]
		public virtual AsyncIterator<TNew> Select<TNew>([NotNull] Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
		{
			Contract.NotNull(asyncSelector, nameof(asyncSelector));

			return AsyncEnumerable.Map<TResult, TNew>(this, new AsyncTransformExpression<TResult,TNew>(asyncSelector));
		}

		[NotNull]
		public virtual AsyncIterator<TNew> SelectMany<TNew>([NotNull] Func<TResult, IEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector, nameof(selector));

			return AsyncEnumerable.Flatten<TResult, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TNew>>(selector));
		}

		[NotNull]
		public virtual AsyncIterator<TNew> SelectMany<TNew>([NotNull] Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
		{
			Contract.NotNull(asyncSelector, nameof(asyncSelector));

			return AsyncEnumerable.Flatten<TResult, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TNew>>(asyncSelector));
		}

		[NotNull]
		public virtual AsyncIterator<TNew> SelectMany<TCollection, TNew>([NotNull] Func<TResult, IEnumerable<TCollection>> collectionSelector, [NotNull] Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(collectionSelector, nameof(collectionSelector));
			Contract.NotNull(resultSelector, nameof(resultSelector));

			return AsyncEnumerable.Flatten<TResult, TCollection, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TCollection>>(collectionSelector), resultSelector);
		}

		[NotNull]
		public virtual AsyncIterator<TNew> SelectMany<TCollection, TNew>([NotNull] Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, [NotNull] Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(asyncCollectionSelector, nameof(asyncCollectionSelector));
			Contract.NotNull(resultSelector, nameof(resultSelector));

			return AsyncEnumerable.Flatten<TResult, TCollection, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TCollection>>(asyncCollectionSelector), resultSelector);
		}

		[NotNull]
		public virtual AsyncIterator<TResult> Take(int count)
		{
			return AsyncEnumerable.Limit<TResult>(this, count);
		}

		[NotNull]
		public virtual AsyncIterator<TResult> TakeWhile([NotNull] Func<TResult, bool> condition)
		{
			return AsyncEnumerable.Limit<TResult>(this, condition);
		}

		[NotNull]
		public virtual AsyncIterator<TResult> Skip(int count)
		{
			return AsyncEnumerable.Offset<TResult>(this, count);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		[NotNull]
		public virtual Task ExecuteAsync([NotNull] Action<TResult> action, CancellationToken ct)
		{
			return AsyncEnumerable.Run<TResult>(this, AsyncIterationHint.All, action, ct);
		}

		[NotNull]
		public virtual Task ExecuteAsync([NotNull] Func<TResult, CancellationToken, Task> asyncAction, CancellationToken ct)
		{
			return AsyncEnumerable.Run<TResult>(this, AsyncIterationHint.All, asyncAction, ct);
		}

		#endregion

		#region Iterator Impl...

		protected abstract Task<bool> OnFirstAsync();

		protected abstract Task<bool> OnNextAsync();

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
			if (Volatile.Read(ref m_state) == STATE_INIT)
			{ // nothing should have been done by the iterator..
				Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_INIT);
			}
			else if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_ITERATING) == STATE_ITERATING)
			{ // the iterator has done at least something, so we can clean it up
				Cleanup();
			}
			return false;
		}

		/// <summary>Mark the current iterator as failed, and clean up the state</summary>
		protected void MarkAsFailed()
		{
			//TODO: store the state "failed" somewhere?
			Dispose();
		}

		protected bool Canceled()
		{
			//TODO: store the state "canceled" somewhere?
			Dispose();
			m_ct.ThrowIfCancellationRequested(); // should throw here!
			return false; //note: should not be reached
		}

		protected void ThrowInvalidState()
		{
			switch (Volatile.Read(ref m_state))
			{
				case STATE_SEQ:
					throw new InvalidOperationException("The async iterator should have been initiliazed with a call to GetEnumerator()");

				case STATE_ITERATING:
					break;

				case STATE_DISPOSED:
					throw new ObjectDisposedException(null, "The async iterator has already been closed");

				default:
				{
					throw new InvalidOperationException();
				}
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

}

#endif
