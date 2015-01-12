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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Native;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;

	/// <summary>FDBFuture[] wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	internal sealed class FdbFutureArray<T> : FdbFuture<T[]>
	{
		// Wraps several FDBFuture* handles and return all the results at once

		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle[] m_handles;

		/// <summary>Counter of callbacks that still need to fire.</summary>
		private int m_pending;

		/// <summary>Lambda used to extract the result of this FDBFuture</summary>
		private readonly Func<FutureHandle, T> m_resultSelector;

		#endregion

		#region Constructors...

		internal FdbFutureArray([NotNull] FutureHandle[] handles, [NotNull] Func<FutureHandle, T> selector, CancellationToken cancellationToken)
		{
			if (handles == null) throw new ArgumentNullException("handles");
			if (handles.Length == 0) throw new ArgumentException("Handle array cannot be empty", "handles");
			if (selector == null) throw new ArgumentNullException("selector");

			m_handles = handles;
			m_resultSelector = selector;

			bool abortAllHandles = false;

			try
			{
				if (cancellationToken.IsCancellationRequested)
				{ // already cancelled, we must abort everything

					SetFlag(FdbFuture.Flags.COMPLETED);
					abortAllHandles = true;
					m_resultSelector = null;
					this.TrySetCanceled();
					return;
				}

				// add this instance to the list of pending futures
				var prm = RegisterCallback(this);

				foreach (var handle in handles)
				{

					if (FdbNative.FutureIsReady(handle))
					{ // this handle is already done
						continue;
					}

					Interlocked.Increment(ref m_pending);

					// register the callback handler
					var err = FdbNative.FutureSetCallback(handle, CallbackHandler, prm);
					if (Fdb.Failed(err))
					{ // uhoh
						Debug.WriteLine("Failed to set callback for Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " !!!");
						throw Fdb.MapToException(err);
					}
				}

				// allow the callbacks to handle completion
				TrySetFlag(FdbFuture.Flags.READY);

				if (Volatile.Read(ref m_pending) == 0)
				{ // all callbacks have already fired (or all handles were already completed)
					UnregisterCallback(this);
					HandleCompletion(fromCallback: false);
					m_resultSelector = null;
					abortAllHandles = true;
					SetFlag(FdbFuture.Flags.COMPLETED);
				}
				else  if (cancellationToken.CanBeCanceled)
				{ // register for cancellation (if needed)
					RegisterForCancellation(cancellationToken);
				}
			}
			catch
			{
				// this is bad news, since we are in the constructor, we need to clear everything
				SetFlag(FdbFuture.Flags.DISPOSED);

				UnregisterCancellationRegistration();

				UnregisterCallback(this);

				abortAllHandles = true;

				// this is technically not needed, but just to be safe...
				this.TrySetCanceled();

				throw;
			}
			finally
			{
				if (abortAllHandles)
				{
					CloseHandles(handles);
				}
			}
			GC.SuppressFinalize(this);
		}

		#endregion

		protected override void CloseHandles()
		{
			CloseHandles(m_handles);
		}

		protected override void CancelHandles()
		{
			CancelHandles(m_handles);
		}

		protected override void ReleaseMemory()
		{
			var handles = m_handles;
			if (handles != null)
			{
				foreach (var handle in handles)
				{
					if (handle != null && !handle.IsClosed && !handle.IsInvalid)
					{
						//REVIEW: there is a possibility of a race condition with Dispoe() that could potentially call FutureDestroy(handle) at the same time (not verified)
						FdbNative.FutureReleaseMemory(handle);
					}
				}
			}
		}

		private static void CloseHandles(FutureHandle[] handles)
		{
			if (handles != null)
			{
				foreach (var handle in handles)
				{
					if (handle != null)
					{
						//note: Dispose() will be a no-op if already called
						handle.Dispose();
					}
				}
			}
		}

		private static void CancelHandles(FutureHandle[] handles)
		{
			if (handles != null)
			{
				foreach (var handle in handles)
				{
					if (handle != null && !handle.IsClosed && !handle.IsInvalid)
					{
						//REVIEW: there is a possibility of a race condition with Dispoe() that could potentially call FutureDestroy(handle) at the same time (not verified)
						FdbNative.FutureCancel(handle);
					}
				}
			}
		}

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

			var future = (FdbFutureArray<T>)GetFutureFromCallbackParameter(parameter);

			if (future != null && Interlocked.Decrement(ref future.m_pending) == 0)
			{ // the last future handle has fired, we can proceed to read all the results

				if (future.HasFlag(FdbFuture.Flags.READY))
				{
					UnregisterCallback(future);
					try
					{
						future.HandleCompletion(fromCallback: true);
					}
					catch(Exception)
					{
						//TODO ?
					}
				}
				// else, the ctor will handle that
			}
		}

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <param name="fromCallback">If true, the method is called from the network thread and must defer the continuations from the Thread Pool</param>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private void HandleCompletion(bool fromCallback)
		{
			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED))
			{
				return;
			}

#if DEBUG_FUTURES
			Debug.WriteLine("FutureArray<" + typeof(T).Name + ">.Callback(...) handling completion on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());
#endif

			try
			{
				UnregisterCancellationRegistration();

				List<Exception> errors = null;
				bool cancellation = false;
				var selector = m_resultSelector;

				var results = selector != null ? new T[m_handles.Length] : null;

				for (int i = 0; i < m_handles.Length; i++)
				{
					var handle = m_handles[i];

					if (handle != null && !handle.IsClosed && !handle.IsInvalid)
					{
						FdbError err = FdbNative.FutureGetError(handle);
						if (Fdb.Failed(err))
						{ // it failed...
							if (err != FdbError.OperationCancelled)
							{ // get the exception from the error code
								var ex = Fdb.MapToException(err);
								(errors ?? (errors = new List<Exception>())).Add(ex);
							}
							else
							{
								cancellation = true;
								break;
							}
						}
						else
						{ // it succeeded...
							// try to get the result...
							if (selector != null)
							{
								//note: result selector will execute from network thread, but this should be our own code that only calls into some fdb_future_get_XXXX(), which should be safe...
								results[i] = selector(handle);
							}
						}
					}
				}

				if (cancellation)
				{ // the transaction has been cancelled
					SetCanceled(fromCallback);
				}
				else if (errors != null)
				{ // there was at least one error
					SetFaulted(errors, fromCallback);
				}
				else
				{  // success
					SetResult(results, fromCallback);
				}

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
				TryCleanup();
			}
		}

	}

}
