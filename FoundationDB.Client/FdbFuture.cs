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

		internal static class Flags
		{
			/// <summary>The future has completed (either success or failure)</summary>
			public const int COMPLETED = 1;

			/// <summary>A completion/failure/cancellation has been posted on the thread pool</summary>
			public const int HAS_POSTED_ASYNC_COMPLETION = 2;

			/// <summary>The future has been cancelled from an external source (manually, or via then CancellationTokeb)</summary>
			public const int CANCELED = 4;

			/// <summary>Dispose has been called</summary>
			public const int DISPOSED = 128;
		}

		/// <summary>Create a new FdbFuture&lt;<typeparamref name="T"/>&gt; from an FDBFuture* pointer</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="selector">Func that will be called to get the result once the future completes (and did not fail)</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of the FDBFuture handle</returns>
		internal static FdbFuture<T> FromHandle<T>(FutureHandle handle, Func<FutureHandle, T> selector, CancellationToken ct)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			return new FdbFuture<T>(handle, handle.IsInvalid ? null : selector, ct);
		}

		/// <summary>Wrap a FdbFuture&lt;<typeparamref name="T"/>&gt; handle into a Task&lt;<typeparamref name="T"/>&gt;</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="continuation">Lambda that will be called once the future completes sucessfully, to extract the result from the future handle.</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Task that will either return the result of the continuation lambda, or an exception</returns>
		internal static Task<T> CreateTaskFromHandle<T>(FutureHandle handle, Func<FutureHandle, T> continuation, CancellationToken ct)
		{
			return FromHandle(handle, continuation, ct).Task;
		}

	}

	/// <summary>FDBFuture wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	[DebuggerDisplay("Flags={m_flags}, State={this.Task.Status}")]
	public class FdbFuture<T> : TaskCompletionSource<T>, IDisposable
	{
		// Wraps a FDBFuture* handle and handles the lifetime of the object

		#region Private Members...

		/// <summary>Flags of the future (bit field of FLAG_xxx values)</summary>
		private int m_flags;

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle m_handle;

		/// <summary>Lambda used to extract the result of this FDBFuture</summary>
		private Func<FutureHandle, T> m_resultSelector;

		/// <summary>Future key in the callback dictionary</summary>
		private volatile IntPtr m_key;

		/// <summary>Optionnal registration on the parent Cancellation Token</summary>
		/// <remarks>Is only valid if FLAG_HAS_CTR is set</remarks>
		private CancellationTokenRegistration m_ctr;

		#endregion

		#region Constructors...

		internal FdbFuture(FutureHandle handle, Func<FutureHandle, T> selector, CancellationToken ct)
		{
			if (handle == null) throw new ArgumentNullException("handle");
			if (selector == null) throw new ArgumentNullException("selector");

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
					return;
				}

				// register for cancellation (if needed)
				if (ct.CanBeCanceled)
				{
					if (ct.IsCancellationRequested)
					{ // we have already been canceled

#if DEBUG_FUTURES
						Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " will complete later");
#endif

						// Abort the future and simulate a Canceled task
						SetFlag(FdbFuture.Flags.COMPLETED);
						// note: we don't need to call fdb_future_cancel because fdb_future_destroy will take care of everything
						m_handle.Dispose();
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

				// add this instance to the list of pending futures
				var prm = RegisterCallback(this);

				// register the callback handler
				var err = FdbNative.FutureSetCallback(handle, CallbackHandler, prm);
				if (Fdb.Failed(err))
				{ // uhoh
					Debug.WriteLine("Failed to set callback for Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " !!!");
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
			GC.SuppressFinalize(this);
		}

		#endregion

		#region State management...

		internal bool HasFlag(int flag)
		{
			return (Volatile.Read(ref m_flags) & flag) == flag;
		}

		internal bool HasAnyFlags(int flags)
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
			//note: if the token is already cancelled, the callback handler will run inline and any exception would bubble up here
			//=> this is not a problem because the ctor already has a try/catch that will clean up everything
			m_ctr = ct.Register(
				(_state) => { CancellationHandler(_state); },
				this,
				false
			);
		}

		private void UnregisterCancellationRegistration()
		{
			// unsubscribe from the parent cancellation token if there was one
			m_ctr.Dispose();
			m_ctr = default(CancellationTokenRegistration);
		}

		private void SetResult(T result, bool fromCallback)
		{
			if (!fromCallback)
			{
				this.TrySetResult(result);
			}
			else if (TrySetFlag(FdbFuture.Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostCompletionOnThreadPool(this, result);
			}
		}

		private void SetFaulted(Exception e, bool fromCallback)
		{
			if (!fromCallback)
			{
				this.TrySetException(e);
			}
			else if (TrySetFlag(FdbFuture.Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostFailureOnThreadPool(this, e);
				// and if it fails ?
			}
		}

		private void SetCanceled(bool fromCallback)
		{
			if (!fromCallback)
			{
				this.TrySetCanceled();
			}
			else if (TrySetFlag(FdbFuture.Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostCancellationOnThreadPool(this);
				// and if it fails ?
			}
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

		#region Callback Management...

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
				future.HandleCompletion(fromCallback: true);
			}
		}

		#endregion

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <param name="future">Future that should be ready</param>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private unsafe void HandleCompletion(bool fromCallback)
		{
			// note: if fromCallback is true, we are running on the network thread
			// this means that we have to signal the TCS from the threadpool, if not continuations on the task may run inline.
			// this is very frequent when we are called with await, or ContinueWith(..., TaskContinuationOptions.ExecuteSynchronously)

			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED))
			{
				return;
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

				// most probably the future was canceled or we are shutting down...
				SetCanceled(fromCallback);
			}
			catch (ThreadAbortException)
			{
				SetCanceled(fromCallback);
			}
			catch (Exception e)
			{ // something went wrong
				SetFaulted(e, fromCallback);
			}
			finally
			{
				TryCleanup();
			}
		}

		private bool TryCleanup()
		{
			// We try to cleanup the future handle as soon as possible, meaning as soon as we have the result, or an error, or a cancellation

			if (TrySetFlag(FdbFuture.Flags.COMPLETED))
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
				if (!this.Task.IsCompleted && TrySetFlag(FdbFuture.Flags.HAS_POSTED_ASYNC_COMPLETION))
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

		#endregion

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
			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED | FdbFuture.Flags.CANCELED))
			{
				return;
			}

			if (TrySetFlag(FdbFuture.Flags.CANCELED))
			{
				bool fromCallback = Fdb.IsNetworkThread;
				try
				{
					if (!this.Task.IsCompleted)
					{
						FdbNative.FutureCancel(m_handle);
						SetCanceled(fromCallback);
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
			if (TrySetFlag(FdbFuture.Flags.DISPOSED))
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

		#region Helper Methods...

		private static void PostCompletionOnThreadPool(FdbFuture<T> future, T result)
		{
			ThreadPool.UnsafeQueueUserWorkItem(
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
			ThreadPool.UnsafeQueueUserWorkItem(
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
			ThreadPool.UnsafeQueueUserWorkItem(
				(_state) =>
				{
					((FdbFuture<T>)_state).TrySetCanceled();
				},
				future
			);
		}

		#endregion

	}

}
