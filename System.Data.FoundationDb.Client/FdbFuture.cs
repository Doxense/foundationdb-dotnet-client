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

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.FoundationDb.Client
{

	public delegate void FdbFutureCallback(IntPtr future, IntPtr parameter);

	/// <summary>FDBFuture wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	public struct FdbFuture<T> : IDisposable
	{

		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle m_handle;

		/// <summary>Task that will complete when the FDBFuture completes</summary>
		private readonly TaskCompletionSource<T> m_tcs;

		/// <summary>Func used to extract the result of this FDBFuture</summary>
		private Func<FutureHandle, T> m_resultSelector;

		#endregion

		#region Constructors...

		internal FdbFuture(FutureHandle handle, Func<FutureHandle, T> selector)
		{
			if (handle == null) throw new ArgumentNullException("handle");
			if (selector == null) throw new ArgumentNullException("selector");

			m_handle = handle;
			m_tcs = new TaskCompletionSource<T>();
			m_resultSelector = selector;

			if (!handle.IsInvalid)
			{
				// already completed or failed ?
				if (FdbNativeStub.FutureIsReady(handle/*.Handle*/))
				{ // either got a value or an error
					Debug.WriteLine("Future 0x" + handle.Handle.ToString("x") + " was already complete");
					TrySetTaskResult(ref this);
				}
				else
				{ // we don't know yet, schedule a callback...
					Debug.WriteLine("Future 0x" + handle.Handle.ToString("x") + " will complete later");
					var callback = new FdbFutureCallback(this.CallbackHandler);
					// note: the callback will allocate the future in the heap...
					FdbNativeStub.FutureSetCallback(handle, callback, IntPtr.Zero);
				}
			}
		}

		#endregion

		/// <summary>Create a new FdbFuture&lt;<typeparamref name="T"/>&gt; from an FDBFuture* pointer</summary>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="selector">Func that will be called to get the result once the future completes (and did not fail)</param>
		/// <returns></returns>
		internal static FdbFuture<T> FromHandle(FutureHandle handle, Func<FutureHandle, T> selector)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			return new FdbFuture<T>(handle, handle.IsInvalid ? null : selector);
		}

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <param name="future">Future that should be ready</param>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private static unsafe bool TrySetTaskResult(ref FdbFuture<T> future)
		{
			var handle = future.m_handle;
			try
			{
				if (handle != null && !handle.IsClosed && !handle.IsInvalid)
				{
					if (FdbNativeStub.FutureIsError(handle/*.Handle*/))
					{ // it failed...
						var err = FdbNativeStub.FutureGetError(handle/*.Handle*/);
						if (err != FdbError.Success)
						{ // get the exception from the error code
							future.m_tcs.TrySetException(FdbCore.MapToException(err));
							return false;
						}
						//else: will be handle below
					}
					else
					{ // it succeeded...
						// try to get the result...
						var selector = future.m_resultSelector;
						if (selector != null)
						{
							var result = future.m_resultSelector(handle);
							future.m_tcs.TrySetResult(result);
							return true;
						}
						//else: it will be handle below
					}
				}

				// most probably the future was cancelled or we are shutting down...
				future.m_tcs.TrySetCanceled();
				return false;	
			}
			catch (Exception e)
			{ // something went wrong
				future.m_tcs.TrySetException(e);
				return false;
			}
			finally
			{
				if (handle != null) handle.Dispose();
			}
		}

		public void Dispose()
		{
			if (!m_handle.IsClosed) m_handle.Dispose();
			// ensure that the task does complete
			m_tcs.TrySetCanceled();
		}

		/// <summary>Handler called when a FDBFuture becomes ready</summary>
		/// <param name="futureHandle">Handle on the future that became ready</param>
		/// <param name="parameter">Paramter to the callback (unused)</param>
		private void CallbackHandler(IntPtr futureHandle, IntPtr parameter)
		{
			Debug.WriteLine("Future.Callback(0x" + futureHandle.ToString("x") + ", " + parameter.ToInt64() + ") has fired");

			//TODO verify if this is our handle ?

			TrySetTaskResult(ref this);
		}

		/// <summary>Returns a Task that wraps the FDBFuture</summary>
		/// <remarks>The task will either return the result of the future, or an exception</remarks>
		public Task<T> Task
		{
			get { return m_tcs.Task; }
		}

		/// <summary>Make the Future awaitable</summary>
		public TaskAwaiter<T> GetAwaiter()
		{
			return m_tcs.Task.GetAwaiter();
		}

		/// <summary>Checks if the FDBFuture is ready</summary>
		public bool IsReady
		{
			get
			{
				return !m_handle.IsInvalid && FdbNativeStub.FutureIsReady(m_handle);
			}
		}

		/// <summary>Checks if a ready FDBFuture has failed</summary>
		public bool IsError
		{
			get
			{
				return m_handle.IsInvalid || FdbNativeStub.FutureIsError(m_handle);
			}
		}

		/// <summary>Synchronously wait for the result of the Future (by blocking the current thread)</summary>
		/// <returns>Result of the future, or an exception if it failed</returns>
		public T GetResult()
		{
			var task = m_tcs.Task;
			if (!task.IsCompleted)
			{ // we need to wait for it to become ready

				Debug.WriteLine("calling block until ready...");
				var err = FdbNativeStub.FutureBlockUntilReady(m_handle);
				Debug.WriteLine("returned from block until ready, err:  " + err.ToString());
				if (FdbCore.Failed(err)) throw FdbCore.MapToException(err);

				// the callback may have already fire, but try to do it anyway...
				TrySetTaskResult(ref this);
			}

			// throw underlying exception if it failed
			if (task.Status == TaskStatus.RanToCompletion)
			{
				return task.Result;
			}

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
