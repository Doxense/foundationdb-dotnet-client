#region Copyright Doxense SAS 2013-2019
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Linq.Iterators
{
	using System;
	using System.Collections.Generic;
	using System.Threading;

	/// <summary>Base class for all async iterators</summary>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	public abstract class Iterator<TResult> : IEnumerable<TResult>, IEnumerator<TResult>
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

		#region IEnumerable<TResult>...

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public IEnumerator<TResult> GetEnumerator()
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

		protected abstract Iterator<TResult> Clone();

		#endregion

		#region IEnumerator<TResult>...

		void System.Collections.IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		object System.Collections.IEnumerator.Current => this.Current;

		public TResult Current
		{
			get
			{
				if (Volatile.Read(ref m_state) != STATE_ITERATING) ThrowInvalidState();
				return m_current;
			}
		}

		public bool MoveNext()
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

			try
			{
				if (state == STATE_INIT)
				{
					if (!OnFirst())
					{ // did not start at all ?
						return Completed();
					}

					if (Interlocked.CompareExchange(ref m_state, STATE_ITERATING, STATE_INIT) != STATE_INIT)
					{ // something happened while we where starting ?
						return false;
					}
				}

				return OnNext();
			}
			catch (Exception)
			{
				MarkAsFailed();
				throw;
			}
		}

		#endregion

		#region Iterator Impl...

		protected abstract bool OnFirst();

		protected abstract bool OnNext();

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
			Dispose();
		}

		protected void ThrowInvalidState()
		{
			switch (Volatile.Read(ref m_state))
			{
				case STATE_SEQ:
					throw new InvalidOperationException("The async iterator should have been initialized with a call to GetEnumerator()");

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
					m_current = default!;
				}
			}
		}

		#endregion
	}

}
