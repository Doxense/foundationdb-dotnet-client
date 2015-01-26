﻿#region BSD Licence
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
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Helper class to create FDBFutures</summary>
	internal static class FdbFuture
	{

		public static class Flags
		{
			/// <summary>The future has completed (either success or failure)</summary>
			public const int COMPLETED = 1;

			/// <summary>A completion/failure/cancellation has been posted on the thread pool</summary>
			public const int HAS_POSTED_ASYNC_COMPLETION = 2;

			/// <summary>The future has been cancelled from an external source (manually, or via then CancellationTokeb)</summary>
			public const int CANCELLED = 4;

			/// <summary>The resources allocated by this future have been released</summary>
			public const int MEMORY_RELEASED = 8;

			/// <summary>The future has been constructed, and is listening for the callbacks</summary>
			public const int READY = 64;

			/// <summary>Dispose has been called</summary>
			public const int DISPOSED = 128;
		}

		/// <summary>Create a new <see cref="FdbFutureSingle{T}"/> from an FDBFuture* pointer</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="selector">Func that will be called to get the result once the future completes (and did not fail)</param>
		/// <param name="cancellationToken">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of the FDBFuture handle</returns>
		[NotNull]
		public static FdbFutureSingle<T> FromHandle<T>([NotNull] FutureHandle handle, [NotNull] Func<FutureHandle, T> selector, CancellationToken cancellationToken)
		{
			return new FdbFutureSingle<T>(handle, selector, cancellationToken);
		}

		/// <summary>Create a new <see cref="FdbFutureArray{T}"/> from an array of FDBFuture* pointers</summary>
		/// <typeparam name="T">Type of the items of the arrayreturn by the task</typeparam>
		/// <param name="handles">Array of FDBFuture* pointers</param>
		/// <param name="selector">Func that will be called for each future that complete (and did not fail)</param>
		/// <param name="cancellationToken">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of all the FDBFuture handles</returns>
		[NotNull]
		public static FdbFutureArray<T> FromHandleArray<T>([NotNull] FutureHandle[] handles, [NotNull] Func<FutureHandle, T> selector, CancellationToken cancellationToken)
		{
			return new FdbFutureArray<T>(handles, selector, cancellationToken);
		}

		/// <summary>Wrap a FDBFuture* pointer into a <see cref="Task{T}"/></summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="continuation">Lambda that will be called once the future completes sucessfully, to extract the result from the future handle.</param>
		/// <param name="cancellationToken">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Task that will either return the result of the continuation lambda, or an exception</returns>
		public static Task<T> CreateTaskFromHandle<T>([NotNull] FutureHandle handle, [NotNull] Func<FutureHandle, T> continuation, CancellationToken cancellationToken)
		{
			return new FdbFutureSingle<T>(handle, continuation, cancellationToken).Task;
		}

		/// <summary>Wrap multiple <see cref="FdbFuture{T}"/> handles into a single <see cref="Task{TResult}"/> that returns an array of T</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handles">Array of FDBFuture* pointers</param>
		/// <param name="continuation">Lambda that will be called once for each future that completes sucessfully, to extract the result from the future handle.</param>
		/// <param name="cancellationToken">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Task that will either return all the results of the continuation lambdas, or an exception</returns>
		/// <remarks>If at least one future fails, the whole task will fail.</remarks>
		public static Task<T[]> CreateTaskFromHandleArray<T>([NotNull] FutureHandle[] handles, [NotNull] Func<FutureHandle, T> continuation, CancellationToken cancellationToken)
		{
			// Special case, because FdbFutureArray<T> does not support empty arrays
			//TODO: technically, there is no reason why FdbFutureArray would not accept an empty array. We should simplify this by handling the case in the ctor (we are already allocating something anyway...)
			if (handles.Length == 0) return Task.FromResult<T[]>(new T[0]);

			return new FdbFutureArray<T>(handles, continuation, cancellationToken).Task;
		}

	}

	/// <summary>Base class for all FDBFuture wrappers</summary>
	/// <typeparam name="T">Type of the Task's result</typeparam>
	[DebuggerDisplay("Flags={m_flags}, State={this.Task.Status}")]
	internal abstract class FdbFuture<T> : TaskCompletionSource<T>, IDisposable
	{

		#region Private Members...

		/// <summary>Flags of the future (bit field of FLAG_xxx values)</summary>
		private int m_flags;

		/// <summary>Future key in the callback dictionary</summary>
		protected IntPtr m_key;

		/// <summary>Optionnal registration on the parent Cancellation Token</summary>
		/// <remarks>Is only valid if FLAG_HAS_CTR is set</remarks>
		protected CancellationTokenRegistration m_ctr;

		#endregion

		#region State Management...

		internal bool HasFlag(int flag)
		{
			return (Volatile.Read(ref m_flags) & flag) == flag;
		}

		internal bool HasAnyFlags(int flags)
		{
			return (Volatile.Read(ref m_flags) & flags) != 0;
		}

		protected void SetFlag(int flag)
		{
			var flags = m_flags;
			Thread.MemoryBarrier();
			m_flags = flags | flag;
		}

		protected bool TrySetFlag(int flag)
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

		protected bool TryCleanup()
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
				CloseHandles();
			}
		}

		/// <summary>Close all the handles managed by this future</summary>
		protected abstract void CloseHandles();

		/// <summary>Cancel all the handles managed by this future</summary>
		protected abstract void CancelHandles();

		/// <summary>Release all memory allocated by this future</summary>
		protected abstract void ReleaseMemory();

		/// <summary>Set the result of this future</summary>
		/// <param name="result">Result of the future</param>
		/// <param name="fromCallback">If true, called from the network thread callback and will defer the operation on the ThreadPool. If false, may run the continuations inline.</param>
		protected void SetResult(T result, bool fromCallback)
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

		/// <summary>Fault the future's Task</summary>
		/// <param name="e">Error that will be the result of the task</param>
		/// <param name="fromCallback">If true, called from the network thread callback and will defer the operation on the ThreadPool. If false, may run the continuations inline.</param>
		protected void SetFaulted(Exception e, bool fromCallback)
		{
			if (!fromCallback)
			{
				this.TrySetException(e);
			}
			else if (TrySetFlag(FdbFuture.Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostFailureOnThreadPool(this, e);
			}
		}

		/// <summary>Fault the future's Task</summary>
		/// <param name="errors">Error that will be the result of the task</param>
		/// <param name="fromCallback">If true, called from the network thread callback and will defer the operation on the ThreadPool. If false, may run the continuations inline.</param>
		protected void SetFaulted(IEnumerable<Exception> errors, bool fromCallback)
		{
			if (!fromCallback)
			{
				this.TrySetException(errors);
			}
			else if (TrySetFlag(FdbFuture.Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostFailureOnThreadPool(this, errors);
			}
		}

		/// <summary>Cancel the future's Task</summary>
		/// <param name="fromCallback">If true, called from the network thread callback and will defer the operation on the ThreadPool. If false, may run the continuations inline.</param>
		protected void SetCanceled(bool fromCallback)
		{
			if (!fromCallback)
			{
				this.TrySetCanceled();
			}
			else if (TrySetFlag(FdbFuture.Flags.HAS_POSTED_ASYNC_COMPLETION))
			{
				PostCancellationOnThreadPool(this);
			}
		}

		/// <summary>Defer setting the result of a TaskCompletionSource on the ThreadPool</summary>
		private static void PostCompletionOnThreadPool(TaskCompletionSource<T> future, T result)
		{
			ThreadPool.UnsafeQueueUserWorkItem(
				(_state) =>
				{
					var prms = (Tuple<TaskCompletionSource<T>, T>)_state;
					prms.Item1.TrySetResult(prms.Item2);
				},
				Tuple.Create(future, result)
			);
		}

		/// <summary>Defer failing a TaskCompletionSource on the ThreadPool</summary>
		private static void PostFailureOnThreadPool(TaskCompletionSource<T> future, Exception error)
		{
			ThreadPool.UnsafeQueueUserWorkItem(
				(_state) =>
				{
					var prms = (Tuple<TaskCompletionSource<T>, Exception>)_state;
					prms.Item1.TrySetException(prms.Item2);
				},
				Tuple.Create(future, error)
			);
		}

		/// <summary>Defer failing a TaskCompletionSource on the ThreadPool</summary>
		private static void PostFailureOnThreadPool(TaskCompletionSource<T> future, IEnumerable<Exception> errors)
		{
			ThreadPool.UnsafeQueueUserWorkItem(
				(_state) =>
				{
					var prms = (Tuple<TaskCompletionSource<T>, IEnumerable<Exception>>)_state;
					prms.Item1.TrySetException(prms.Item2);
				},
				Tuple.Create(future, errors)
			);
		}

		/// <summary>Defer cancelling a TaskCompletionSource on the ThreadPool</summary>
		private static void PostCancellationOnThreadPool(TaskCompletionSource<T> future)
		{
			ThreadPool.UnsafeQueueUserWorkItem(
				(_state) => ((TaskCompletionSource<T>)_state).TrySetCanceled(),
				future
			);
		}

		#endregion

		#region Callbacks...

		/// <summary>List of all pending futures that have not yet completed</summary>
		private static readonly ConcurrentDictionary<IntPtr, FdbFuture<T>> s_futures = new ConcurrentDictionary<IntPtr, FdbFuture<T>>();

		/// <summary>Internal counter to generated a unique parameter value for each futures</summary>
		private static long s_futureCounter;

		/// <summary>Register a future in the callback context and return the corresponding callback parameter</summary>
		/// <param name="future">Future instance</param>
		/// <returns>Parameter that can be passed to FutureSetCallback and that uniquely identify this future.</returns>
		/// <remarks>The caller MUST call ClearCallbackHandler to ensure that the future instance is removed from the list</remarks>
		internal static IntPtr RegisterCallback([NotNull] FdbFuture<T> future)
		{
			Contract.Requires(future != null);

			// generate a new unique id for this future, that will be use to lookup the future instance in the callback handler
			long id = Interlocked.Increment(ref s_futureCounter);
			var prm = new IntPtr(id); // note: we assume that we can only run in 64-bit mode, so it is safe to cast a long into an IntPtr
			// critical region
			try { }
			finally
			{
				Volatile.Write(ref future.m_key, prm);
#if DEBUG_FUTURES
				Contract.Assert(!s_futures.ContainsKey(prm));
#endif
				s_futures[prm] = future;
				Interlocked.Increment(ref DebugCounters.CallbackHandlesTotal);
				Interlocked.Increment(ref DebugCounters.CallbackHandles);
			}
			return prm;
		}

		/// <summary>Remove a future from the callback handler dictionary</summary>
		/// <param name="future">Future that has just completed, or is being destroyed</param>
		internal static void UnregisterCallback([NotNull] FdbFuture<T> future)
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
					FdbFuture<T> _;
					if (s_futures.TryRemove(key, out _))
					{
						Interlocked.Decrement(ref DebugCounters.CallbackHandles);
					}
				}
			}
		}

		internal static FdbFuture<T> GetFutureFromCallbackParameter(IntPtr parameter)
		{
			FdbFuture<T> future;
			if (s_futures.TryGetValue(parameter, out future))
			{
				if (future != null && Volatile.Read(ref future.m_key) == parameter)
				{
					return future;
				}
#if DEBUG_FUTURES
				// If you breakpoint here, that means that a future callback fired but was not able to find a matching registration
				// => either the FdbFuture<T> was incorrectly disposed, or there is some problem in the callback dictionary
				if (System.Diagnostics.Debugger.IsAttached)  System.Diagnostics.Debugger.Break();
#endif
			}
			return null;
		}

		#endregion

		#region Cancellation...

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
		}

		/// <summary>Free memory allocated by this future after it has completed.</summary>
		/// <remarks>This method provides no benefit to most application code, and should only be called when attempting to write thread-safe custom layers.</remarks>
		public void Clear()
		{
			if (HasFlag(FdbFuture.Flags.DISPOSED))
			{
				return;
			}

			if (!this.Task.IsCompleted)
			{
				throw new InvalidOperationException("Cannot release memory allocated by a future that has not yet completed");
			}

			if (TrySetFlag(FdbFuture.Flags.MEMORY_RELEASED))
			{
				ReleaseMemory();
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
					if (Volatile.Read(ref m_key) != IntPtr.Zero) UnregisterCallback(this);
				}
			}
			GC.SuppressFinalize(this);
		}

	}

}
