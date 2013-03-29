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
	* Neither the name of the <organization> nor the
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

// try enabling this to diagnose problems with fdb_future_block_until_ready hanging...
#undef WORKAROUND_USE_POLLING

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using FoundationDb.Client.Native;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client
{

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FdbFutureCallback(IntPtr future, IntPtr parameter);

	/// <summary>Helper class to create FDBFutures</summary>
	public static class FdbFuture
	{
		/// <summary>Create a new FdbFuture&lt;<typeparamref name="T"/>&gt; from an FDBFuture* pointer</summary>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="selector">Func that will be called to get the result once the future completes (and did not fail)</param>
		/// <returns></returns>
		internal static FdbFuture<T> FromHandle<T>(FutureHandle handle, Func<FutureHandle, T> selector, CancellationToken ct, bool willBlockForResult)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			return new FdbFuture<T>(handle, handle.IsInvalid ? null : selector, ct, willBlockForResult);
		}

		internal static Task<T> CreateTaskFromHandle<T>(FutureHandle handle, Func<FutureHandle, T> continuation, CancellationToken ct)
		{
			return FromHandle(handle, continuation, ct, willBlockForResult: false).AsTask();
		}

	}

	/// <summary>FDBFuture wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	public class FdbFuture<T> : IDisposable
	{
		//EXPERIMENTAL: defined as a struct to try and remove some memory allocations...

		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle m_handle;

		/// <summary>Task that will complete when the FDBFuture completes</summary>
		private readonly TaskCompletionSource<T> m_tcs;

		/// <summary>Func used to extract the result of this FDBFuture</summary>
		private Func<FutureHandle, T> m_resultSelector;

		private ExecutionContext m_context;

		/// <summary>Used to pin the callback handler.</summary>
		/// <remarks>It should be alive at least as long has the future handle, so only call Free() after fdb_future_destroy !</remarks>
		private GCHandle m_callback;

		private int m_flags;

		private CancellationTokenRegistration m_ctr;

		private const int DISPOSED = 1;
		private const int BLOCKING = 2;

		#endregion

		#region Constructors...

		internal FdbFuture(FutureHandle handle, Func<FutureHandle, T> selector, CancellationToken ct, bool willBlockForResult)
		{
			if (handle == null) throw new ArgumentNullException("handle");
			if (selector == null) throw new ArgumentNullException("selector");

			ct.ThrowIfCancellationRequested();

			m_handle = handle;
			m_tcs = new TaskCompletionSource<T>();
			m_resultSelector = selector;
			m_callback = default(GCHandle);
			m_flags = 0;
			m_context = null;

			if (handle.IsInvalid)
			{ // it's dead, Jim !
				m_flags |= DISPOSED;
			}
			else
			{
				if (FdbNative.FutureIsReady(handle))
				{ // either got a value or an error
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " was already ready");
					TrySetTaskResult(fromCallback: false);
				}
				else if (willBlockForResult)
				{ // this future will be consumed synchronously, no need to schedule a callback
					m_flags |= BLOCKING;

					if (ct.CanBeCanceled) RegisterForCancellation(ct);
				}
				else
				{ // we don't know yet, schedule a callback...
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " will complete later");

					if (ct.CanBeCanceled) RegisterForCancellation(ct);

					// pin the callback to prevent it from behing garbage collected
					var callback = new FdbFutureCallback(this.CallbackHandler);
					m_callback = GCHandle.Alloc(callback);

					try
					{
						// note: the callback will allocate the future in the heap...
						var err = FdbNative.FutureSetCallback(handle, callback, IntPtr.Zero);
						//TODO: schedule some sort of timeout ?

						if (FdbCore.Failed(err))
						{ // uhoh
							Debug.WriteLine("Failed to set callback for Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " !!!");
							var error = FdbCore.MapToException(err);
							m_tcs.TrySetException(error);
							throw error;
						}
					}
					catch (Exception)
					{
						Cleanup();
						throw;
					}

				}
			}
		}

		#endregion

		private void RegisterForCancellation(CancellationToken ct)
		{
			m_ctr = ct.Register(
				(_state) => { CancellationHandler(_state); },
				this,
				false
			);
		}

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <param name="future">Future that should be ready</param>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private unsafe bool TrySetTaskResult(bool fromCallback)
		{
			// note: if fromCallback is true, we are running on the network thread
			// this means that we have to signal the TCS from the threadpool, if not continuations on the task may run inline.
			// this is very frequent when we are called with await, or ContinueWith(..., TaskContinuationOptions.ExecuteSynchronously)

			try
			{
				var handle = m_handle;
				if (handle != null && !handle.IsClosed && !handle.IsInvalid)
				{
					m_ctr.Dispose();

					if (FdbNative.FutureIsError(handle))
					{ // it failed...
						Debug.WriteLine("Future<" + typeof(T).Name + "> has FAILED");
						var err = FdbNative.FutureGetError(handle);
						if (err != FdbError.Success)
						{ // get the exception from the error code
							var ex = FdbCore.MapToException(err);
							if (fromCallback)
								TrySetExceptionFromThreadPool(m_tcs, ex);
							else
								m_tcs.TrySetException(ex);
							return false;
						}
						//else: will be handle below
					}
					else
					{ // it succeeded...
						// try to get the result...
						Debug.WriteLine("Future<" + typeof(T).Name + "> has completed successfully");
						var selector = m_resultSelector;
						if (selector != null)
						{
							//note: result selector will execute from network thread, but this should be our own code that only calls into some fdb_future_get_XXXX(), which should be safe...
							var result = m_resultSelector(handle);
							if (fromCallback)
								TrySetResultFromThreadPool(m_tcs, result);
							else
								m_tcs.TrySetResult(result);
							return true;
						}
						//else: it will be handled below
					}
				}

				// most probably the future was cancelled or we are shutting down...
				if (fromCallback)
					TrySetCancelledFromThreadPool(m_tcs);
				else
					m_tcs.TrySetCanceled();
				return false;
			}
			catch (ThreadAbortException)
			{
				if (fromCallback)
					TrySetCancelledFromThreadPool(m_tcs);
				else
					m_tcs.TrySetCanceled();
				return false;
			}
			catch (Exception e)
			{ // something went wrong
				if (fromCallback)
					TrySetExceptionFromThreadPool(m_tcs, e);
				else
					m_tcs.TrySetException(e);
				return false;
			}
			finally
			{
				Cleanup();
			}
		}

		private void TrySetResultFromThreadPool(TaskCompletionSource<T> tcs, T result)
		{
			//TODO: try to not allocate a scope
			var context = m_context;
			if (context != null)
			{
				ExecutionContext.Run(context, (_) => { tcs.TrySetResult(result); }, null);
			}
			else
			{
				ThreadPool.QueueUserWorkItem((_) => { tcs.TrySetResult(result); }, null);
			}
		}

		private static void TrySetExceptionFromThreadPool(TaskCompletionSource<T> tcs, Exception e)
		{
			//TODO: try to not allocate a scope
			Task.Run(() => { tcs.TrySetException(e); });
		}

		private static void TrySetCancelledFromThreadPool(TaskCompletionSource<T> tcs)
		{
			//TODO: try to not allocate a scope
			Task.Run(() => { tcs.TrySetCanceled(); });
		}

		private void Cleanup()
		{
			var flags = m_flags;
			if ((flags & DISPOSED) == 0 && Interlocked.CompareExchange(ref m_flags, flags | DISPOSED, flags) == flags)
			{
				m_ctr.Dispose();
				if (!m_handle.IsClosed) m_handle.Dispose();
				if (m_callback.IsAllocated) m_callback.Free();
				if (m_tcs.Task.IsCompleted)
				{ // ensure that the task always complete
					m_tcs.TrySetCanceled();
				}
				m_context = null;
			}
		}

		/// <summary>Try to abort the task</summary>
		public void Abort()
		{
			try
			{
				if (!m_tcs.Task.IsCompleted)
				{
					if (FdbCore.IsNetworkThread)
						TrySetCancelledFromThreadPool(m_tcs);
					else
						m_tcs.TrySetCanceled();
				}
			}
			finally
			{
				Cleanup();
			}
		}

		void IDisposable.Dispose()
		{
			Cleanup();
		}

		/// <summary>Handler called when a FDBFuture becomes ready</summary>
		/// <param name="futureHandle">Handle on the future that became ready</param>
		/// <param name="parameter">Paramter to the callback (unused)</param>
		private void CallbackHandler(IntPtr futureHandle, IntPtr parameter)
		{
			Debug.WriteLine("Future<" + typeof(T).Name + ">.Callback(0x" + futureHandle.ToString("x") + ", " + parameter.ToString("x") + ") has fired on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());

			//TODO verify if this is our handle ?

			try
			{
				// note: we are always called from the network thread
				TrySetTaskResult(fromCallback: true);
			}
			finally
			{
				Cleanup();
			}
		}

		private static void CancellationHandler(object state)
		{
			var future = (FdbFuture<T>)state;

			Debug.WriteLine("Future<" + typeof(T).Name + ">.Cancell(0x" + future.m_handle.Handle.ToString("x") + ") was called on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());

			future.Abort();
		}

		/// <summary>Returns a Task that wraps the FDBFuture</summary>
		/// <remarks>The task will either return the result of the future, or an exception</remarks>
		public Task<T> AsTask()
		{
			if ((m_flags & BLOCKING) != 0) throw new InvalidOperationException("This Future can only be used synchronously!");
			return m_tcs.Task;
		}

		/// <summary>Make the Future awaitable</summary>
		public TaskAwaiter<T> GetAwaiter()
		{
			if ((m_flags & BLOCKING) != 0) throw new InvalidOperationException("This Future can only be used synchronously!");
			return m_tcs.Task.GetAwaiter();
		}

		/// <summary>Checks if the FDBFuture is ready</summary>
		public bool IsReady
		{
			get
			{
				return !m_handle.IsInvalid && FdbNative.FutureIsReady(m_handle);
			}
		}

		/// <summary>Checks if a ready FDBFuture has failed</summary>
		public bool IsError
		{
			get
			{
				return m_handle.IsInvalid || FdbNative.FutureIsError(m_handle);
			}
		}

		/// <summary>Synchronously wait for the result of the Future (by blocking the current thread)</summary>
		/// <returns>Result of the future, or an exception if it failed</returns>
		public T GetResult()
		{
			var task = m_tcs.Task;
			if (!task.IsCompleted)
			{ // we need to wait for it to become ready

				FdbCore.EnsureNotOnNetworkThread();

#if WORKAROUND_USE_POLLING
				var max = DateTime.UtcNow.AddSeconds(5);
				while (!FdbNativeStub.FutureIsReady(m_handle))
				{
					Debug.WriteLine("Future<" + typeof(T).Name + ">(0x" + m_handle.Handle.ToString("x") + ") still not ready. Waiting...");
					Thread.Sleep(500);
					if (DateTime.UtcNow >= max)
					{ // uhoh
						Debug.WriteLine("Future<" + typeof(T).Name + ">(0x" + m_handle.Handle.ToString("x") + ") has timed out and will be aborted");
						m_tcs.TrySetException(new TimeoutException());
						Cleanup();
						goto failure; // so sue me !
					}
				}
#else
				//note: in beta1, this will block forever if there is less then 5% free disk space on db partition... :(
				var err = FdbNative.FutureBlockUntilReady(m_handle);
				if (FdbCore.Failed(err)) throw FdbCore.MapToException(err);
#endif

				// the callback may have already fire, but try to do it anyway...
				TrySetTaskResult(fromCallback: false);
			}

			// throw underlying exception if it failed
			if (task.Status == TaskStatus.RanToCompletion)
			{
				return task.Result;
			}

#if WORKAROUND_USE_POLLING
		failure:
#endif

			// try to preserve the callstack by using the awaiter to throw
			// note: calling task.Result would throw an AggregateException that is not nice to work with...
			return m_tcs.Task.GetAwaiter().GetResult();
		}

		/// <summary>Synchronously wait for a Future to complete (by blocking the current thread)</summary>
		public void Wait()
		{
			if (m_tcs.Task.Status != TaskStatus.RanToCompletion)
			{
				var _ = GetResult();
			}
		}

	}

}
