#region BSD License
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

//#define DEBUG_FUTURES

namespace FoundationDB.Client.Native
{
	using System;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>FDBFuture wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	internal sealed class FdbFutureSingle<T> : FdbFuture<T>
	{
		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle m_handle;

		/// <summary>Lambda used to extract the result of this FDBFuture</summary>
		private readonly Func<FutureHandle, T> m_resultSelector;

		#endregion

		#region Constructors...

		internal FdbFutureSingle([NotNull] FutureHandle handle, [NotNull] Func<FutureHandle, T> selector, CancellationToken ct)
		{
			Contract.NotNull(handle, nameof(handle));
			Contract.NotNull(selector, nameof(selector));

			m_handle = handle;
			m_resultSelector = selector;

			try
			{

				if (handle.IsInvalid)
				{ // it's dead, Jim !
					SetFlag(FdbFuture.Flags.COMPLETED);
					m_resultSelector = null;
					return;
				}

				if (FdbNative.FutureIsReady(handle))
				{ // either got a value or an error
#if DEBUG_FUTURES
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " was already ready");
#endif
					HandleCompletion(fromCallback: false);
#if DEBUG_FUTURES
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " completed inline");
#endif
					return;
				}

				// register for cancellation (if needed)
				if (ct.CanBeCanceled)
				{
					if (ct.IsCancellationRequested)
					{ // we have already been cancelled

#if DEBUG_FUTURES
						Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " will complete later");
#endif

						// Abort the future and simulate a Canceled task
						SetFlag(FdbFuture.Flags.COMPLETED);
						// note: we don't need to call fdb_future_cancel because fdb_future_destroy will take care of everything
						handle.Dispose();
						// also, don't keep a reference on the callback because it won't be needed
						m_resultSelector = null;
						this.TrySetCanceled();
						return;
					}

					// token still active
					RegisterForCancellation(ct);
				}

#if DEBUG_FUTURES
				Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " will complete later");
#endif

				TrySetFlag(FdbFuture.Flags.READY);

				// add this instance to the list of pending futures
				var prm = RegisterCallback(this);

				// register the callback handler
				var err = FdbNative.FutureSetCallback(handle, CallbackHandler, prm);
				if (Fdb.Failed(err))
				{ // uhoh
#if DEBUG_FUTURES
					Debug.WriteLine("Failed to set callback for Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " !!!");
#endif
					throw Fdb.MapToException(err);
				}
			}
			catch
			{
				// this is bad news, since we are in the constructor, we need to clear everything
				SetFlag(FdbFuture.Flags.DISPOSED);
				UnregisterCancellationRegistration();
				UnregisterCallback(this);

				// kill the future handle
				m_handle.Dispose();

				// this is technically not needed, but just to be safe...
				this.TrySetCanceled();

				throw;
			}
			GC.KeepAlive(this);
		}

		#endregion

		/// <summary>Cached delegate of the future completion callback handler</summary>
		private static readonly FdbNative.FdbFutureCallback CallbackHandler = FutureCompletionCallback;

		/// <summary>Handler called when a FDBFuture becomes ready</summary>
		/// <param name="futureHandle">Handle on the future that became ready</param>
		/// <param name="parameter">Paramter to the callback (unused)</param>
		private static void FutureCompletionCallback(IntPtr futureHandle, IntPtr parameter)
		{
#if DEBUG_FUTURES
			Debug.WriteLine("Future<" + typeof(T).Name + ">.Callback(0x" + futureHandle.ToString("x") + ", " + parameter.ToString("x") + ") has fired on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());
#endif

			var future = (FdbFutureSingle<T>)GetFutureFromCallbackParameter(parameter);
			if (future != null)
			{
				UnregisterCallback(future);
				future.HandleCompletion(fromCallback: true);
			}
		}

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <param name="fromCallback">If true, we are called from the network thread</param>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private void HandleCompletion(bool fromCallback)
		{
			// note: if fromCallback is true, we are running on the network thread
			// this means that we have to signal the TCS from the threadpool, if not continuations on the task may run inline.
			// this is very frequent when we are called with await, or ContinueWith(..., TaskContinuationOptions.ExecuteSynchronously)

			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED))
			{
				return;
			}

#if DEBUG_FUTURES
			var sw = Stopwatch.StartNew();
#endif
			try
			{
				var handle = m_handle;
				if (handle != null && !handle.IsClosed && !handle.IsInvalid)
				{
					UnregisterCancellationRegistration();

					FdbError err = FdbNative.FutureGetError(handle);
					if (Fdb.Failed(err))
					{ // it failed...
#if DEBUG_FUTURES
						Debug.WriteLine("Future<" + typeof(T).Name + "> has FAILED: " + err);
#endif
						if (err != FdbError.OperationCancelled)
						{ // get the exception from the error code
							var ex = Fdb.MapToException(err);
							SetFaulted(ex, fromCallback);
							return;
						}
						//else: will be handle below
					}
					else
					{ // it succeeded...
						// try to get the result...
#if DEBUG_FUTURES
						Debug.WriteLine("Future<" + typeof(T).Name + "> has completed successfully");
#endif
						var selector = m_resultSelector;
						if (selector != null)
						{
							//note: result selector will execute from network thread, but this should be our own code that only calls into some fdb_future_get_XXXX(), which should be safe...
							var result = selector(handle);
							SetResult(result, fromCallback);
							return;
						}
						//else: it will be handled below
					}
				}

				// most probably the future was cancelled or we are shutting down...
				SetCanceled(fromCallback);
			}
			catch (Exception e)
			{ // something went wrong
				if (e is ThreadAbortException)
				{
					SetCanceled(fromCallback);
					throw;
				}
				SetFaulted(e, fromCallback);
			}
			finally
			{
#if DEBUG_FUTURES
				sw.Stop();
				Debug.WriteLine("Future<" + typeof(T).Name + "> callback completed in " + sw.Elapsed.TotalMilliseconds.ToString() + " ms");
#endif
				TryCleanup();
			}
		}

		protected override void CloseHandles()
		{
			var handle = m_handle;
			if (handle != null) handle.Dispose();
		}

		protected override void CancelHandles()
		{
			var handle = m_handle;
			//REVIEW: there is a possibility of a race condition with Dispose() that could potentially call FutureDestroy(handle) at the same time (not verified)
			if (handle != null && !handle.IsClosed && !handle.IsInvalid) FdbNative.FutureCancel(handle);
		}

		protected override void ReleaseMemory()
		{
			var handle = m_handle;
			//REVIEW: there is a possibility of a race condition with Dispose() that could potentially call FutureDestroy(handle) at the same time (not verified)
			if (handle != null && !handle.IsClosed && !handle.IsInvalid) FdbNative.FutureReleaseMemory(handle);
		}

	}

}
