#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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

// enable this to help debug Futures
#undef DEBUG_FUTURES

namespace FoundationDB.Client.Native
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;

	/// <summary>Base class for all FDBFuture wrappers</summary>
	/// <typeparam name="T">Type of the Task's result</typeparam>
	[DebuggerDisplay("Label={Label}, Cookie={Cookie}, State={Task.Status}")]
	internal abstract class FdbFuture<T> : TaskCompletionSource<T>, IFdbFuture
	{

		#region Private Members...

		///// <summary>Optionnal registration on the parent Cancellation Token</summary>
		///// <remarks>Is only valid if FLAG_HAS_CTR is set</remarks>
		//protected CancellationTokenRegistration m_ctr;

		protected FdbFuture(IntPtr cookie, string label)
		{
			this.Cookie = cookie;
			this.Label = label;
		}

		public IntPtr Cookie { get; private set; }

		public string Label { get; private set; }

		#endregion

		#region Cancellation...

#if REFACTORED

		protected void RegisterForCancellation(CancellationToken cancellationToken)
		{
			//note: if the token is already cancelled, the callback handler will run inline and any exception would bubble up here
			//=> this is not a problem because the ctor already has a try/catch that will clean up everything
			m_ctr = cancellationToken.Register(
				(_state) => { CancellationHandler(_state); },
				this,
				false
			);
		}

		protected void UnregisterCancellationRegistration()
		{
			// unsubscribe from the parent cancellation token if there was one
			m_ctr.Dispose();
			m_ctr = default(CancellationTokenRegistration);
		}

		private static void CancellationHandler(object state)
		{
			var future = state as FdbFuture<T>;
			if (future != null)
			{
#if DEBUG_FUTURES
				Debug.WriteLine("Future<" + typeof(T).Name + ">.Cancel(0x" + future.m_handle.Handle.ToString("x") + ") was called on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());
#endif
				future.Cancel();
			}
		}

#endif

		#endregion

		public abstract bool Visit(IntPtr handle);

		public abstract void OnFired();

		/// <summary>Return true if the future has completed (successfully or not)</summary>
		public bool IsReady
		{
			get { return this.Task.IsCompleted; }
		}

		/// <summary>Make the Future awaitable</summary>
		public TaskAwaiter<T> GetAwaiter()
		{
			return this.Task.GetAwaiter();
		}

		/// <summary>Try to abort the task (if it is still running)</summary>
		public void Cancel()
		{
			throw new NotImplementedException("FIXME: Future Cancellation!");
#if REFACTORED
			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED | FdbFuture.Flags.CANCELLED))
			{
				return;
			}

			if (TrySetFlag(FdbFuture.Flags.CANCELLED))
			{
				bool fromCallback = Fdb.IsNetworkThread;
				try
				{
					if (!this.Task.IsCompleted)
					{
						CancelHandles();
						SetCanceled(fromCallback);
					}
				}
				finally
				{
					TryCleanup();
				}
			}
#endif
		}

	}

}
