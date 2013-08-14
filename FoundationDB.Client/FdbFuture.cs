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

// enable this to help debug Futures
#undef DEBUG_FUTURES

// to allow blocking methods on Futures
#undef ENABLE_BLOCKING_CALLS

namespace FoundationDB.Client
{
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;

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
	[DebuggerDisplay("Flags={m_flags}, State={this.Task.Status}")]
	public class FdbFuture<T> : TaskCompletionSource<T>, IDisposable
	{
		// Wraps a FDBFuture* handle and handles the lifetime of the object

		private static class Flags
		{
			/// <summary>The future as completed (either success or failure)</summary>
			public const int COMPLETED = 1;

			/// <summary>The future as been registered for cancellation with a parent CancellationToken</summary>
			public const int HAS_CTR = 2;

			/// <summary>A completion/failure/cancellation has been posted on the thread pool</summary>
			public const int HAS_POSTED_ASYNC_COMPLETION = 4;

			public const int CANCELED = 8;

#if ENABLE_BLOCKING_CALLS
			/// <summary>The future is configured in blocking mode (no callback)</summary>
			public const int BLOCKING = 64;
#endif

			/// <summary>Dispose has been called</summary>
			public const int DISPOSED = 128;
		}

		#region Private Members...

		/// <summary>Flags of the future (bit field of FLAG_xxx values)</summary>
		private int m_flags;

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle m_handle;

		/// <summary>Func used to extract the result of this FDBFuture</summary>
		private Func<FutureHandle, T> m_resultSelector;

		/// <summary>Future key in the callback dictionary</summary>
		private volatile IntPtr m_key;

		/// <summary>Optionnal registration on the parent Cancellation Token</summary>
		/// <remarks>Is only valid if FLAG_HAS_CTR is set</remarks>
		private CancellationTokenRegistration m_ctr;

		#endregion

		#region Constructors...

		internal FdbFuture(FutureHandle handle, Func<FutureHandle, T> selector, CancellationToken ct, bool willBlockForResult)
		{
			if (handle == null) throw new ArgumentNullException("handle");
			if (selector == null) throw new ArgumentNullException("selector");

			m_handle = handle;
			m_resultSelector = selector;

			try
			{

				if (handle.IsInvalid)
				{ // it's dead, Jim !
					SetFlag(Flags.COMPLETED);
					m_resultSelector = null;
					return;
				}

				if (FdbNative.FutureIsReady(handle))
				{ // either got a value or an error
#if DEBUG_FUTURES
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " was already ready");
#endif
					TrySetTaskResult(fromCallback: false);
					return;
				}

				if (ct.IsCancellationRequested)
				{ // we have already been canceled

					// Abort the future and simulate a Canceled task
					SetFlag(Flags.COMPLETED);
					m_resultSelector = null;
					m_handle.Dispose();
					this.TrySetCanceled();
					return;
				}

				// register for cancellation (if needed)
				RegisterForCancellation(ct);

				if (willBlockForResult)
				{ // this future will be consumed synchronously, no need to schedule a callback
#if ENABLE_BLOCKING_CALLS
					SetFlag(Flags.BLOCKING);
#else
					throw new InvalidOperationException("Non-async Futures are not supported in this version");
#endif
				}
				else
				{ // we don't know yet, schedule a callback...
#if DEBUG_FUTURES
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " will complete later");
#endif

					// add this instance to the list of pending futures
					IntPtr prm = RegisterCallback(this);

					// register the callback handler
					var err = FdbNative.FutureSetCallback(handle, CallbackHandler, prm);
					if (Fdb.Failed(err))
					{ // uhoh
						Debug.WriteLine("Failed to set callback for Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " !!!");
						throw Fdb.MapToException(err);
					}
				}
			}
			catch (Exception)
			{
				// this is bad news, since we are in the constructor, we need to clear everything
				SetFlag(Flags.DISPOSED);
				UnregisterCancellationRegistration();
				UnregisterCallback(this);
				this.TrySetCanceled();

				throw;
			}
			GC.SuppressFinalize(this);
		}

		#endregion

		#region State management...

		private bool HasFlag(int flag)
		{
			return (Volatile.Read(ref m_flags) & flag) == flag;
		}

		private bool HasAnyFlags(int flags)
		{
			return (Volatile.Read(ref m_flags) & flags) != 0;
		}

		private void SetFlag(int flag)
		{
			var flags = m_flags;
			Thread.MemoryBarrier();
			m_flags = flags | flag;
		}

		private bool TrySetFlag(int flag)
		{
			var wait = new SpinWait();
			while (true)
			{
				var flags = Volatile.Read(ref m_flags);
				if ((flags & flag) != 0)
				{
					return false;
				}
				if (Interlocked.CompareExchange(ref m_flags, flags | flag, flags) == flags)
				{
					return true;
				}
				wait.SpinOnce();
			}

		}

		private void RegisterForCancellation(CancellationToken ct)
		{
			if (!ct.CanBeCanceled) return;
			try
			{
				SetFlag(Flags.HAS_CTR);
				m_ctr = ct.Register(
					(_state) => { CancellationHandler(_state); },
					this,
					false
				);
			}
			catch(Exception)
			{
				m_ctr.Dispose();
				throw;
			}
		}

		private void UnregisterCancellationRegistration()
		{
			// unsubscribe from the parent cancellation token if there was one
			if ((Volatile.Read(ref m_flags) & Flags.HAS_CTR) != 0)
			{
				m_ctr.Dispose();
				m_ctr = default(CancellationTokenRegistration);
			}
		}

#if false
		private void ClearCallback()
		{
			if (m_callback.IsAllocated)
			{
				m_callback.Free();
#if DEBUG
				Interlocked.Decrement(ref DebugCounters.CallbackHandles);
#endif
			}
		}
#endif

		private void TrySetException(Exception e, bool fromCallback)
		{
			if (!fromCallback)
			{
				this.TrySetException(e);
			}
			else if (TrySetFlag(Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostFailureOnThreadPool(this, e);
				// and if it fails ?
			}
		}

		private void TrySetCanceled(bool fromCallback)
		{
#if DEBUG_FUTURES
			Debug.WriteLine("TrySetCanceled(" + fromCallback + ")");
#endif

			if (!fromCallback)
			{
				this.TrySetCanceled();
			}
			else if (TrySetFlag(Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostCancellationOnThreadPool(this);
				// and if it fails ?
			}
		}

		private static void CancellationHandler(object state)
		{
			var future = (FdbFuture<T>)state;

#if DEBUG_FUTURES
			Debug.WriteLine("Future<" + typeof(T).Name + ">.Cancel(0x" + future.m_handle.Handle.ToString("x") + ") was called on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());
#endif

			future.Cancel();
		}

		/// <summary>List of all pending futures that have not yet completed</summary>
		private static readonly ConcurrentDictionary<IntPtr, FdbFuture<T>> s_futures = new ConcurrentDictionary<IntPtr, FdbFuture<T>>();

		/// <summary>Internal counter to generated a unique parameter value for each futures</summary>
		private static long s_futureCounter = 0;

		/// <summary>Register a future in the callback context and return the corresponding callback parameter</summary>
		/// <param name="future">Future instance</param>
		/// <returns>Parameter that can be passed to FutureSetCallback and that uniquely identify this future.</returns>
		/// <remarks>The caller MUST call ClearCallbackHandler to ensure that the future instance is removed from the list</remarks>
		private static IntPtr RegisterCallback(FdbFuture<T> future)
		{
			Contract.Requires(future != null);

			// generate a new unique id for this future, that will be use to lookup the future instance in the callback handler
			long id = Interlocked.Increment(ref s_futureCounter);
			var prm = new IntPtr(id); // note: we assume that we can only run in 64-bit mode, so it is safe to cast a long into an IntPtr
			// critical region
			try { }
			finally
			{
				future.m_key = prm;
				s_futures[prm] = future;
#if DEBUG
				Interlocked.Increment(ref DebugCounters.CallbackHandlesTotal);
				Interlocked.Increment(ref DebugCounters.CallbackHandles);
#endif
			}
			return prm;
		}

		/// <summary>Remove a future from the callback handler dictionary</summary>
		/// <param name="future">Future that has just completed, or is being destroyed</param>
		private static void UnregisterCallback(FdbFuture<T> future)
		{
			Contract.Requires(future != null);

			// critical region
			try
			{ }
			finally
			{
				var key = Interlocked.Exchange(ref future.m_key, IntPtr.Zero);
				if (key != IntPtr.Zero)
				{
					FdbFuture<T> instance;
					if (s_futures.TryRemove(key, out instance))
					{
#if DEBUG
						Interlocked.Decrement(ref DebugCounters.CallbackHandles);
#endif
					}
				}
			}
		}

		/// <summary>Cached delegate of the future completion callback handler</summary>
		private static readonly FdbFutureCallback CallbackHandler = new FdbFutureCallback(FutureCompletionCallback);

		/// <summary>Handler called when a FDBFuture becomes ready</summary>
		/// <param name="futureHandle">Handle on the future that became ready</param>
		/// <param name="parameter">Paramter to the callback (unused)</param>
		private static void FutureCompletionCallback(IntPtr futureHandle, IntPtr parameter)
		{
#if DEBUG_FUTURES
			Debug.WriteLine("Future<" + typeof(T).Name + ">.Callback(0x" + futureHandle.ToString("x") + ", " + parameter.ToString("x") + ") has fired on thread #" + Thread.CurrentThread.ManagedThreadId.ToString());
#endif

			FdbFuture<T> future;
			if (s_futures.TryGetValue(parameter, out future)
				&& future != null
				&& future.m_key == parameter)
			{
				UnregisterCallback(future);
				future.TrySetTaskResult(fromCallback: true);
			}
		}

		#endregion

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <param name="future">Future that should be ready</param>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private unsafe bool TrySetTaskResult(bool fromCallback)
		{
			// note: if fromCallback is true, we are running on the network thread
			// this means that we have to signal the TCS from the threadpool, if not continuations on the task may run inline.
			// this is very frequent when we are called with await, or ContinueWith(..., TaskContinuationOptions.ExecuteSynchronously)

			if (HasAnyFlags(Flags.DISPOSED | Flags.COMPLETED))
			{
				return false;
			}

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
						if (err != FdbError.TransactionCancelled && err != FdbError.OperationCancelled)
						{ // get the exception from the error code
							var ex = Fdb.MapToException(err);
							TrySetException(ex, fromCallback);
							return false;
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
							if (!fromCallback)
							{
								this.TrySetResult(result);
							}
							else if (TrySetFlag(Flags.HAS_POSTED_ASYNC_COMPLETION))
							{
								PostCompletionOnThreadPool(this, result);
							}
							return true;
						}
						//else: it will be handled below
					}
				}

				// most probably the future was canceled or we are shutting down...
				TrySetCanceled(fromCallback);
				return false;
			}
			catch (ThreadAbortException)
			{
				TrySetCanceled(fromCallback);
				return false;
			}
			catch (Exception e)
			{ // something went wrong
				TrySetException(e, fromCallback);
				return false;
			}
			finally
			{
				TryCleanup();
			}
		}

		private bool TryCleanup()
		{
			// We try to cleanup the future handle as soon as possible, meaning as soon as we have the result, or an error, or a cancellation

			if (TrySetFlag(Flags.COMPLETED))
			{
				DoCleanup();
				return true;
			}
			return false;
		}

		private void DoCleanup()
		{
			try
			{
				// unsubscribe from the parent cancellation token if there was one
				UnregisterCancellationRegistration();

				// ensure that the task always complete !
				// note: always defer the completion on the threadpool, because we don't want to dead lock here (we can be called by Dispose)
				if (!this.Task.IsCompleted && TrySetFlag(Flags.HAS_POSTED_ASYNC_COMPLETION))
				{
					PostCancellationOnThreadPool(this);
				}

				// The only surviving value after this would be a Task and an optional WorkItem on the ThreadPool that will signal it...
			}
			finally
			{
				if (!m_handle.IsClosed) m_handle.Dispose();
				m_resultSelector = null;
			}
		}

		private void ClearCallback()
		{
			if (m_key != IntPtr.Zero)
			{
				UnregisterCallback(this);
			}
		}

		/// <summary>Try to abort the task (if it is still running)</summary>
		public void Cancel()
		{
			if (HasAnyFlags(Flags.DISPOSED | Flags.COMPLETED | Flags.CANCELED))
			{
				return;
			}

			if (TrySetFlag(Flags.CANCELED))
			{
				bool fromCallback = Fdb.IsNetworkThread;
				try
				{
					if (!this.Task.IsCompleted)
					{
						FdbNative.FutureCancel(m_handle);
						TrySetCanceled(fromCallback);
					}
				}
				finally
				{
					TryCleanup();
				}
			}
		}

		public void Dispose()
		{
			if (TrySetFlag(Flags.DISPOSED))
			{
				try
				{
					TryCleanup();
				}
				finally
				{
					if (m_key != IntPtr.Zero) UnregisterCallback(this);
				}
			}
			GC.SuppressFinalize(this);
		}

		/// <summary>Returns a Task that wraps the FDBFuture</summary>
		/// <remarks>The task will either return the result of the future, or an exception</remarks>
		public Task<T> AsTask()
		{
#if ENABLE_BLOCKING_CALLS
			if ((Volatile.Read(ref m_flags) & Flags.BLOCKING) != 0) throw new InvalidOperationException("This Future can only be used synchronously!");
#endif
			return this.Task;
		}

		/// <summary>Make the Future awaitable</summary>
		public TaskAwaiter<T> GetAwaiter()
		{
			return AsTask().GetAwaiter();
		}

#if ENABLE_BLOCKING_CALLS

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
			if (!this.Task.IsCompleted)
			{ // we need to wait for it to become ready

				Fdb.EnsureNotOnNetworkThread();

				//note: in beta2, this will block forever if there is less then 5% free disk space on db partition... :(
				var err = FdbNative.FutureBlockUntilReady(m_handle);
				if (Fdb.Failed(err)) throw Fdb.MapToException(err);

				// the callback may have already fire, but try to do it anyway...
				TrySetTaskResult(fromCallback: false);
			}

			// throw underlying exception if it failed
			if (this.Task.Status == TaskStatus.RanToCompletion)
			{
				return this.Task.Result;
			}

			// try to preserve the callstack by using the awaiter to throw
			// note: calling task.Result would throw an AggregateException that is not nice to work with...
			return this.Task.GetAwaiter().GetResult();
		}

		/// <summary>Synchronously wait for a Future to complete (by blocking the current thread)</summary>
		public void Wait()
		{
			if (this.Task.Status != TaskStatus.RanToCompletion)
			{
				var _ = GetResult();
			}
		}

#endif

		#region Helper Methods...

		private static void PostCompletionOnThreadPool(FdbFuture<T> future, T result)
		{
			ThreadPool.QueueUserWorkItem(
				(_state) =>
				{
					var prms = (Tuple<FdbFuture<T>, T>)_state;
					prms.Item1.TrySetResult(prms.Item2);
				},
				Tuple.Create(future, result)
			);
		}

		private static void PostFailureOnThreadPool(FdbFuture<T> future, Exception error)
		{
			ThreadPool.QueueUserWorkItem(
				(_state) =>
				{
					var prms = (Tuple<FdbFuture<T>, Exception>)_state;
					prms.Item1.TrySetException(prms.Item2);
				},
				Tuple.Create(future, error)
			);
		}

		private static void PostCancellationOnThreadPool(FdbFuture<T> future)
		{
			ThreadPool.QueueUserWorkItem(
				(_state) =>
				{
					((TaskCompletionSource<T>)_state).TrySetCanceled();
				},
				future
			);
		}

		#endregion

	}

}
