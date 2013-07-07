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

			public virtual AsyncIterator<TResult> Where(Func<TResult, bool> predicate)
			{
				if (predicate == null) throw new ArgumentNullException("predicate");

				return FdbAsyncEnumerable.Filter<TResult>(this, predicate);
			}

			public virtual AsyncIterator<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
			{
				if (asyncPredicate == null) throw new ArgumentNullException("asyncPredicate");

				return FdbAsyncEnumerable.Filter<TResult>(this, asyncPredicate);
			}

			public virtual AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
			{
				if (selector == null) throw new ArgumentNullException("selector");

				return FdbAsyncEnumerable.Map<TResult, TNew>(this, selector);
			}

			public virtual AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
			{
				if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

				return FdbAsyncEnumerable.Map<TResult, TNew>(this, asyncSelector);
			}

			public virtual AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
			{
				if (selector == null) throw new ArgumentNullException("selector");

				return FdbAsyncEnumerable.Flatten<TResult, TNew>(this, selector);
			}

			public virtual AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
			{
				if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

				return FdbAsyncEnumerable.Flatten<TResult, TNew>(this, asyncSelector);
			}

			public virtual AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
			{
				if (collectionSelector == null) throw new ArgumentNullException("collectionSelector");
				if (resultSelector == null) throw new ArgumentNullException("resultSelector");

				return FdbAsyncEnumerable.Flatten<TResult, TCollection, TNew>(this, collectionSelector, resultSelector);
			}

			public virtual AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TResult, TCollection, TNew> resultSelector)
			{
				if (asyncCollectionSelector == null) throw new ArgumentNullException("asyncCollectionSelector");
				if (resultSelector == null) throw new ArgumentNullException("resultSelector");

				return FdbAsyncEnumerable.Flatten<TResult, TCollection, TNew>(this, asyncCollectionSelector, resultSelector);
			}

			public virtual AsyncIterator<TResult> Take(int limit)
			{
				return FdbAsyncEnumerable.Limit<TResult>(this, limit);
			}

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

	}
}
