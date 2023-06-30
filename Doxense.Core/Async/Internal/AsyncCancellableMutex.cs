#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Async
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Implements a async mutex that supports cancellation</summary>
	[DebuggerDisplay("Status={this.Task.Status}, CancellationState=({m_state}, {m_ct.IsCancellationRequested?\"alive\":\"cancelled\"})")]
	public class AsyncCancelableMutex : TaskCompletionSource<object?>
	{
		// The consumer just needs to await the Task and will be woken up if someone calls Set(..) / Abort() on the mutex OR if the CancellationToken provided in the ctor is signaled.
		// Optionally, Set() and Abort() can specify if the consumer will be woken up from the ThreadPool (async = true) or probably inline (async = false)

		// note: this is not really a mutex because there is no "Reset()" method (not possible to reset a TCS)...

		private static readonly Action<object?> s_cancellationCallback = CancellationHandler;

		/// <summary>Returns an already completed, new mutex instance</summary>
		private static AsyncCancelableMutex CreateAlreadyDone()
		{
			var mtx = new AsyncCancelableMutex(CancellationToken.None);
			mtx.Set(async: false);
			return mtx;
		}

		/// <summary>Mutex that has already completed</summary>
		public static AsyncCancelableMutex AlreadyDone { get; } = CreateAlreadyDone();

		private const int STATE_NONE = 0;
		private const int STATE_SET = 1;
		private const int STATE_CANCELED = 2;

		private int m_state;
		private CancellationTokenRegistration m_ctr;

		/// <summary>Handler called if the CancellationToken linked to a waiter is signaled</summary>
		/// <param name="state"></param>
		private static void CancellationHandler(object? state)
		{
			// the state contains the weak reference on the waiter, that we need to unwrap...

			var weakRef = (WeakReference<AsyncCancelableMutex>) state!;
			if (weakRef.TryGetTarget(out AsyncCancelableMutex waiter))
			{ // still alive...
				waiter.Abort(async: true);
			}
		}

		public AsyncCancelableMutex(CancellationToken ct)
		{
			if (ct.CanBeCanceled)
			{
				m_ctr = ct.Register(s_cancellationCallback, new WeakReference<AsyncCancelableMutex>(this), useSynchronizationContext: false);
			}
			GC.SuppressFinalize(this);
		}

		public bool IsCompleted => m_state != 0;

		public bool Set(bool async = false)
		{
			if (Interlocked.CompareExchange(ref m_state, STATE_SET, STATE_NONE) != STATE_NONE)
			{ // someone beat us to it
				return false;
			}
			m_ctr.Dispose();

			if (async)
			{
				SetDeferred(this);
			}
			else
			{
				TrySetResult(null);
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
				CancelDeferred(this);
			}
			else
			{
				this.TrySetCanceled();
			}
			return true;
		}

		private static void SetDeferred(AsyncCancelableMutex mutex)
		{
			ThreadPool.QueueUserWorkItem((state) => ((AsyncCancelableMutex)state).TrySetResult(null), mutex);
		}

		private static void CancelDeferred(AsyncCancelableMutex mutex)
		{
			ThreadPool.QueueUserWorkItem((state) => ((AsyncCancelableMutex)state).TrySetCanceled(), mutex);
		}

	}

}
