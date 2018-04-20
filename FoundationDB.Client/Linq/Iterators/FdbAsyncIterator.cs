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
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Base class for all async iterators</summary>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal abstract class FdbAsyncIterator<TResult> : IFdbAsyncEnumerable<TResult>, IFdbAsyncEnumerator<TResult>
	{
		//REVIEW: we could need an IFdbAsyncIterator<T> interface that holds all the Select(),Where(),Take(),... so that it can be used by FdbAsyncEnumerable to either call them directly (if the query supports it) or use a generic implementation
		// => this would be implemented by FdbAsyncIterator<T> as well as FdbRangeQuery<T> (and ony other 'self optimizing' class)

		private const int STATE_SEQ = 0;
		private const int STATE_INIT = 1;
		private const int STATE_ITERATING = 2;
		private const int STATE_COMPLETED = 3;
		private const int STATE_DISPOSED = -1;

		protected TResult m_current;
		protected int m_state;
		protected FdbAsyncMode m_mode;

		#region IFdbAsyncEnumerable<TResult>...

		public IAsyncEnumerator<TResult> GetEnumerator()
		{
			return this.GetEnumerator(FdbAsyncMode.Default);
		}

		public IFdbAsyncEnumerator<TResult> GetEnumerator(FdbAsyncMode mode)
		{
			// reuse the same instance the first time
			if (Interlocked.CompareExchange(ref m_state, STATE_INIT, STATE_SEQ) == STATE_SEQ)
			{
				m_mode = mode;
				return this;
			}
			// create a new one
			var iter = Clone();
			iter.m_mode = mode;
			Volatile.Write(ref iter.m_state, STATE_INIT);
			return iter;
		}

		protected abstract FdbAsyncIterator<TResult> Clone();

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

		public async Task<bool> MoveNextAsync(CancellationToken ct)
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
				return Canceled(ct);
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
			catch (Exception)
			{
				MarkAsFailed();
				throw;
			}
		}

		#endregion

		#region LINQ...

		[NotNull]
		public virtual FdbAsyncIterator<TResult> Where([NotNull] Func<TResult, bool> predicate)
		{
			if (predicate == null) throw new ArgumentNullException("predicate");

			return FdbAsyncEnumerable.Filter<TResult>(this, new AsyncFilterExpression<TResult>(predicate));
		}

		[NotNull]
		public virtual FdbAsyncIterator<TResult> Where([NotNull] Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			if (asyncPredicate == null) throw new ArgumentNullException("asyncPredicate");

			return FdbAsyncEnumerable.Filter<TResult>(this, new AsyncFilterExpression<TResult>(asyncPredicate));
		}

		[NotNull]
		public virtual FdbAsyncIterator<TNew> Select<TNew>([NotNull] Func<TResult, TNew> selector)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			return FdbAsyncEnumerable.Map<TResult, TNew>(this, new AsyncTransformExpression<TResult,TNew>(selector));
		}

		[NotNull]
		public virtual FdbAsyncIterator<TNew> Select<TNew>([NotNull] Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
		{
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return FdbAsyncEnumerable.Map<TResult, TNew>(this, new AsyncTransformExpression<TResult,TNew>(asyncSelector));
		}

		[NotNull]
		public virtual FdbAsyncIterator<TNew> SelectMany<TNew>([NotNull] Func<TResult, IEnumerable<TNew>> selector)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			return FdbAsyncEnumerable.Flatten<TResult, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TNew>>(selector));
		}

		[NotNull]
		public virtual FdbAsyncIterator<TNew> SelectMany<TNew>([NotNull] Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
		{
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return FdbAsyncEnumerable.Flatten<TResult, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TNew>>(asyncSelector));
		}

		[NotNull]
		public virtual FdbAsyncIterator<TNew> SelectMany<TCollection, TNew>([NotNull] Func<TResult, IEnumerable<TCollection>> collectionSelector, [NotNull] Func<TResult, TCollection, TNew> resultSelector)
		{
			if (collectionSelector == null) throw new ArgumentNullException("collectionSelector");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			return FdbAsyncEnumerable.Flatten<TResult, TCollection, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TCollection>>(collectionSelector), resultSelector);
		}

		[NotNull]
		public virtual FdbAsyncIterator<TNew> SelectMany<TCollection, TNew>([NotNull] Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, [NotNull] Func<TResult, TCollection, TNew> resultSelector)
		{
			if (asyncCollectionSelector == null) throw new ArgumentNullException("asyncCollectionSelector");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			return FdbAsyncEnumerable.Flatten<TResult, TCollection, TNew>(this, new AsyncTransformExpression<TResult,IEnumerable<TCollection>>(asyncCollectionSelector), resultSelector);
		}

		[NotNull]
		public virtual FdbAsyncIterator<TResult> Take(int count)
		{
			return FdbAsyncEnumerable.Limit<TResult>(this, count);
		}

		[NotNull]
		public virtual FdbAsyncIterator<TResult> TakeWhile([NotNull] Func<TResult, bool> condition)
		{
			return FdbAsyncEnumerable.Limit<TResult>(this, condition);
		}

		[NotNull]
		public virtual FdbAsyncIterator<TResult> Skip(int count)
		{
			return FdbAsyncEnumerable.Offset<TResult>(this, count);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		[NotNull]
		public virtual Task ExecuteAsync([NotNull] Action<TResult> action, CancellationToken ct)
		{
			return FdbAsyncEnumerable.Run<TResult>(this, FdbAsyncMode.All, action, ct);
		}

		[NotNull]
		public virtual Task ExecuteAsync([NotNull] Func<TResult, CancellationToken, Task> asyncAction, CancellationToken ct)
		{
			return FdbAsyncEnumerable.Run<TResult>(this, FdbAsyncMode.All, asyncAction, ct);
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
			if (Volatile.Read(ref m_state) == STATE_INIT)
			{ // nothing should have been done by the iterator..
				Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_INIT);
			}
			else if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_ITERATING) == STATE_ITERATING)
			{ // the iterator has done at least something, so we can clean it up
				this.Cleanup();
			}
			return false;
		}

		/// <summary>Mark the current iterator as failed, and clean up the state</summary>
		protected void MarkAsFailed()
		{
			//TODO: store the state "failed" somewhere?
			this.Dispose();
		}

		protected bool Canceled(CancellationToken cancellationToken)
		{
			//TODO: store the state "canceled" somewhere?
			this.Dispose();
			cancellationToken.ThrowIfCancellationRequested();
			return false;
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
