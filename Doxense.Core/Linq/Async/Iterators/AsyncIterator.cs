#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Linq.Async.Iterators
{
	using Doxense.Linq.Async.Expressions;

	/// <summary>Base class for all async iterators</summary>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	public abstract class AsyncIterator<TResult> : IConfigurableAsyncEnumerable<TResult>, IAsyncEnumerator<TResult>
	{
		//REVIEW: we could need an IAsyncIterator<T> interface that holds all the Select(),Where(),Take(),... so that it can be used by AsyncEnumerable to either call them directly (if the query supports it) or use a generic implementation
		// => this would be implemented by AsyncIterator<T> as well as FdbRangeQuery<T> (and ony other 'self optimizing' class)

		private const int STATE_SEQ = 0;
		private const int STATE_INIT = 1;
		private const int STATE_ITERATING = 2;
		private const int STATE_COMPLETED = 3;
		private const int STATE_DISPOSED = -1;

		protected TResult? m_current;
		protected int m_state;
		protected AsyncIterationHint m_mode;
		protected CancellationToken m_ct;

		#region IAsyncEnumerable<TResult>...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken ct) => GetAsyncEnumerator(ct, AsyncIterationHint.Default);

		public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken ct, AsyncIterationHint mode)
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
				if (Volatile.Read(ref m_state) != STATE_ITERATING) EnsureIsIterating();
				return m_current!;
			}
		}

		public async ValueTask<bool> MoveNextAsync()
		{
			var state = Volatile.Read(ref m_state);

			if (state == STATE_COMPLETED)
			{
				return false;
			}
			if (state != STATE_INIT && state != STATE_ITERATING)
			{
				EnsureIsIterating();
				return false;
			}

			if (m_ct.IsCancellationRequested)
			{
				return await Canceled().ConfigureAwait(false);
			}

			try
			{
				if (state == STATE_INIT)
				{
					if (!await OnFirstAsync().ConfigureAwait(false))
					{ // did not start at all ?
						return await Completed().ConfigureAwait(false);
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
				await MarkAsFailed().ConfigureAwait(false);
				throw;
			}
		}

		#endregion

		#region LINQ...

		public virtual AsyncIterator<TResult> Where(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncEnumerable.Filter(this, new AsyncFilterExpression<TResult>(predicate));
		}

		public virtual AsyncIterator<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			Contract.NotNull(asyncPredicate);

			return AsyncEnumerable.Filter(this, new AsyncFilterExpression<TResult>(asyncPredicate));
		}

		public virtual AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			Contract.NotNull(selector);

			return AsyncEnumerable.Map(this, new AsyncTransformExpression<TResult,TNew>(selector));
		}

		public virtual AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
		{
			Contract.NotNull(asyncSelector);

			return AsyncEnumerable.Map(this, new AsyncTransformExpression<TResult,TNew>(asyncSelector));
		}

		public virtual AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncEnumerable.Flatten(this, new AsyncTransformExpression<TResult,IEnumerable<TNew>>(selector));
		}

		public virtual AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
		{
			Contract.NotNull(asyncSelector);

			return AsyncEnumerable.Flatten(this, new AsyncTransformExpression<TResult,IEnumerable<TNew>>(asyncSelector));
		}

		public virtual AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			return AsyncEnumerable.Flatten(this, new AsyncTransformExpression<TResult,IEnumerable<TCollection>>(collectionSelector), resultSelector);
		}

		public virtual AsyncIterator<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(asyncCollectionSelector);
			Contract.NotNull(resultSelector);

			return AsyncEnumerable.Flatten(this, new AsyncTransformExpression<TResult,IEnumerable<TCollection>>(asyncCollectionSelector), resultSelector);
		}

		public virtual AsyncIterator<TResult> Take(int count)
		{
			return AsyncEnumerable.Limit(this, count);
		}

		public virtual AsyncIterator<TResult> TakeWhile(Func<TResult, bool> condition)
		{
			return AsyncEnumerable.Limit(this, condition);
		}

		public virtual AsyncIterator<TResult> Skip(int count)
		{
			return AsyncEnumerable.Offset(this, count);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task ExecuteAsync(Action<TResult> action, CancellationToken ct)
		{
			return AsyncEnumerable.Run(this, AsyncIterationHint.All, action, ct);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task ExecuteAsync<TState>(TState state, Action<TState, TResult> action, CancellationToken ct)
		{
			return AsyncEnumerable.Run(this, AsyncIterationHint.All, state, action, ct);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> action, CancellationToken ct)
		{
			return AsyncEnumerable.Run(this, AsyncIterationHint.All, seed, action, ct);
		}

		public virtual Task ExecuteAsync(Func<TResult, CancellationToken, Task> asyncAction, CancellationToken ct)
		{
			return AsyncEnumerable.Run(this, AsyncIterationHint.All, asyncAction, ct);
		}

		#endregion

		#region Iterator Impl...

		protected abstract ValueTask<bool> OnFirstAsync();

		protected abstract ValueTask<bool> OnNextAsync();

		protected bool Publish(TResult current)
		{
			if (Volatile.Read(ref m_state) == STATE_ITERATING)
			{
				m_current = current;
				return true;
			}
			return false;
		}

		protected async ValueTask<bool> Completed()
		{
			if (Volatile.Read(ref m_state) == STATE_INIT)
			{ // nothing should have been done by the iterator..
				Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_INIT);
			}
			else if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_ITERATING) == STATE_ITERATING)
			{ // the iterator has done at least something, so we can clean it up
				await Cleanup().ConfigureAwait(false);
			}
			return false;
		}

		/// <summary>Mark the current iterator as failed, and clean up the state</summary>
		protected ValueTask MarkAsFailed()
		{
			//TODO: store the state "failed" somewhere?
			return DisposeAsync();
		}

		protected async ValueTask<bool> Canceled()
		{
			//TODO: store the state "canceled" somewhere?
			await DisposeAsync().ConfigureAwait(false);
			m_ct.ThrowIfCancellationRequested(); // should throw here!
			return false; //note: should not be reached
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		protected void EnsureIsIterating()
		{
			switch (Volatile.Read(ref m_state))
			{
				case STATE_SEQ:
					throw ThrowHelper.InvalidOperationException("The async iterator should have been initialized with a call to GetEnumerator()");

				case STATE_ITERATING:
					break;

				case STATE_DISPOSED:
					throw ThrowHelper.ObjectDisposedException(this, "The async iterator has already been closed");

				default:
				{
					throw ThrowHelper.InvalidOperationException("Unexpected state");
				}
			}
		}

		protected abstract ValueTask Cleanup();

		#endregion

		#region IAsyncDisposable...

		public virtual async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					await Cleanup().ConfigureAwait(false);
				}
				finally
				{
					m_current = default!;
					GC.SuppressFinalize(this);
				}
			}
		}

		#endregion

	}

}
