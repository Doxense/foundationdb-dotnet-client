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

namespace FoundationDB.Async
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Implements a async mutex that supports cancellation</summary>
	[DebuggerDisplay("Status={this.Task.Status}, CancellationState=({m_state}, {m_ct.IsCancellationRequested?\"alive\":\"cancelled\"})")]
	public class AsyncCancelableMutex : TaskCompletionSource<object>
	{
		// The consumer just needs to await the Task and will be woken up if someone calls Set(..) / Abort() on the mutex OR if the CancellationToken provided in the ctor is signaled.
		// Optionally, Set() and Abort() can specify if the consumer will be woken up from the ThreadPool (asnyc = true) or probably inline (async = false)

		// note: this is not really a mutex because there is no "Reset()" method (not possible to reset a TCS)...

		private static readonly Action<object> s_cancellationCallback = CancellationHandler;
		private static readonly AsyncCancelableMutex s_alreadyCompleted = CreateAlreadyDone();

		/// <summary>Returns an already completed, new mutex instance</summary>
		private static AsyncCancelableMutex CreateAlreadyDone()
		{
			var mtx = new AsyncCancelableMutex(CancellationToken.None);
			mtx.Set(async: false);
			return mtx;
		}

		public static AsyncCancelableMutex AlreadyDone
		{
			get { return s_alreadyCompleted; }
		}

		private const int STATE_NONE = 0;
		private const int STATE_SET = 1;
		private const int STATE_CANCELED = 2;

		private int m_state;
		private CancellationTokenRegistration m_ctr;

		/// <summary>Handler called if the CancellationToken linked to a waiter is signaled</summary>
		/// <param name="state"></param>
		private static void CancellationHandler(object state)
		{
			// the state contains the weak reference on the waiter, that we need to unwrap...

			var weakRef = (WeakReference<AsyncCancelableMutex>)state;
			AsyncCancelableMutex waiter;
			if (weakRef.TryGetTarget(out waiter))
			{ // still alive...
				waiter.Abort(async: true);
			}
		}

		public AsyncCancelableMutex(CancellationToken ct)
		{
			if (ct.CanBeCanceled)
			{
				m_ctr = ct.RegisterWithoutEC(s_cancellationCallback, new WeakReference<AsyncCancelableMutex>(this));
			}
			GC.SuppressFinalize(this);
		}

		public bool IsCompleted { get { return m_state != 0; } }

		public bool Set(bool async = false)
		{
			if (Interlocked.CompareExchange(ref m_state, STATE_SET, STATE_NONE) != STATE_NONE)
			{ // someone beat us to it
				return false;
			}
			m_ctr.Dispose();

			if (async)
			{
				SetDefered(this);
			}
			else
			{
				this.TrySetResult(null);
			}
			return true;
		}

		public bool Abort(bool async = false)
		{
			if (Interlocked.CompareExchange(ref m_state, STATE_CANCELED, STATE_NONE) != STATE_NONE)
			{ // someone beat us to it
				return false;
			}
			m_ctr.Dispose();

			if (async)
			{
				CancelDefered(this);
			}
			else
			{
				this.TrySetCanceled();
			}
			return true;
		}

		private static void SetDefered(AsyncCancelableMutex mutex)
		{
			ThreadPool.UnsafeQueueUserWorkItem((state) => ((AsyncCancelableMutex)state).TrySetResult(null), mutex);
		}

		private static void CancelDefered(AsyncCancelableMutex mutex)
		{
			ThreadPool.UnsafeQueueUserWorkItem((state) => ((AsyncCancelableMutex)state).TrySetCanceled(), mutex);
		}

	}

}
