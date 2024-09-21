#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

// enable this to help debug Futures
//#define DEBUG_FUTURES

namespace FoundationDB.Client.Native
{
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using FoundationDB.Client.Utils;

	/// <summary>Helper class to create FDBFutures</summary>
	public static class FdbFuture
	{

		public static class Flags
		{
			/// <summary>The future has completed (either success or failure)</summary>
			public const int COMPLETED = 1;

			/// <summary>The future has been cancelled from an external source (manually, or via then CancellationToken)</summary>
			public const int CANCELLED = 2;

			/// <summary>The resources allocated by this future have been released</summary>
			public const int MEMORY_RELEASED = 4;

			/// <summary>The future has been constructed, and is listening for the callbacks</summary>
			public const int READY = 64;

			/// <summary>Dispose has been called</summary>
			public const int DISPOSED = 128;
		}

		/// <summary>Create a new <see cref="FdbFutureSingle{T}"/> from an FDBFuture* pointer</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="selector">Func that will be called to get the result once the future completes (and did not fail)</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of the FDBFuture handle</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbFutureSingle<T> FromHandle<T>(FutureHandle handle, Func<FutureHandle, T> selector, CancellationToken ct)
		{
			return new FdbFutureSingle<T>(handle, selector, ct);
		}

		/// <summary>Create a new <see cref="FdbFutureArray{T}"/> from an array of FDBFuture* pointers</summary>
		/// <typeparam name="T">Type of the items of the array returned by the task</typeparam>
		/// <param name="handles">Array of FDBFuture* pointers</param>
		/// <param name="selector">Func that will be called for each future that complete (and did not fail)</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of all the FDBFuture handles</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbFutureArray<T> FromHandleArray<T>(FutureHandle[] handles, Func<FutureHandle, T> selector, CancellationToken ct)
		{
			return new FdbFutureArray<T>(handles, selector, ct);
		}

		/// <summary>Wrap a FDBFuture* pointer into a <see cref="Task{T}"/></summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="continuation">Lambda that will be called once the future completes successfully, to extract the result from the future handle.</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Task that will either return the result of the continuation lambda, or an exception</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<T> CreateTaskFromHandle<T>(FutureHandle handle, Func<FutureHandle, T> continuation, CancellationToken ct)
		{
			return new FdbFutureSingle<T>(handle, continuation, ct).Task;
		}

		/// <summary>Wrap multiple <see cref="FdbFuture{T}"/> handles into a single <see cref="Task{TResult}"/> that returns an array of T</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="handles">Array of FDBFuture* pointers</param>
		/// <param name="continuation">Lambda that will be called once for each future that completes successfully, to extract the result from the future handle.</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Task that will either return all the results of the continuation lambdas, or an exception</returns>
		/// <remarks>If at least one future fails, the whole task will fail.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<T[]> CreateTaskFromHandleArray<T>(FutureHandle[] handles, Func<FutureHandle, T> continuation, CancellationToken ct)
		{
			return new FdbFutureArray<T>(handles, continuation, ct).Task;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbFuture<T> Create<T>(CancellationToken ct)
		{
			return new FdbFutureTask<T>(ct);
		}

	}

	/// <summary>Base class for all FDBFuture wrappers</summary>
	/// <typeparam name="T">Type of the Task's result</typeparam>
	[DebuggerDisplay("Flags={m_flags}, State={this.Task.Status}")]
	public abstract class FdbFuture<T> : TaskCompletionSource<T>, IDisposable
	{

		#region Private Members...

		/// <summary>Flags of the future (bit field of FLAG_xxx values)</summary>
		private int m_flags;

		/// <summary>Future key in the callback dictionary</summary>
		protected IntPtr m_key;

		/// <summary>Optional registration on the parent Cancellation Token</summary>
		/// <remarks>Is only valid if FLAG_HAS_CTR is set</remarks>
		protected CancellationTokenRegistration m_ctr;

		#endregion

		protected FdbFuture() : base(TaskCreationOptions.RunContinuationsAsynchronously)
		{ }

		#region State Management...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool HasFlag(int flag) => (Volatile.Read(ref m_flags) & flag) == flag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool HasAnyFlags(int flags) => (Volatile.Read(ref m_flags) & flags) != 0;

		protected void SetFlag(int flag)
		{
			var flags = m_flags;
			Interlocked.MemoryBarrier();
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
				if (!this.Task.IsCompleted)
				{
					TrySetCanceled();
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

		#endregion

		#region Callbacks...

		/// <summary>List of all pending futures that have not yet completed</summary>
		private static readonly ConcurrentDictionary<long, FdbFuture<T>> s_futures = new ConcurrentDictionary<long, FdbFuture<T>>();

		/// <summary>Internal counter to generated a unique parameter value for each futures</summary>
		private static long s_futureCounter;

		/// <summary>Register a future in the callback context and return the corresponding callback parameter</summary>
		/// <param name="future">Future instance</param>
		/// <returns>Parameter that can be passed to FutureSetCallback and that uniquely identify this future.</returns>
		/// <remarks>The caller MUST call ClearCallbackHandler to ensure that the future instance is removed from the list</remarks>
		internal static IntPtr RegisterCallback(FdbFuture<T> future)
		{
			Contract.Debug.Requires(future != null);

			// generate a new unique id for this future, that will be use to lookup the future instance in the callback handler
			long id = Interlocked.Increment(ref s_futureCounter);
			var prm = new IntPtr(id); // note: we assume that we can only run in 64-bit mode, so it is safe to cast a long into an IntPtr
			// critical region
			try { }
			finally
			{
				Volatile.Write(ref future.m_key, prm);
#if DEBUG_FUTURES
				Contract.Debug.Assert(!s_futures.ContainsKey(prm));
#endif
				s_futures[prm.ToInt64()] = future;
				Interlocked.Increment(ref DebugCounters.CallbackHandlesTotal);
				Interlocked.Increment(ref DebugCounters.CallbackHandles);
			}
			return prm;
		}

		/// <summary>Remove a future from the callback handler dictionary</summary>
		/// <param name="future">Future that has just completed, or is being destroyed</param>
		internal static void UnregisterCallback(FdbFuture<T> future)
		{
			Contract.Debug.Requires(future != null);

			// critical region
			try
			{ }
			finally
			{
				var key = Interlocked.Exchange(ref future.m_key, IntPtr.Zero);
				if (key != IntPtr.Zero)
				{
					if (s_futures.TryRemove(key.ToInt64(), out _))
					{
						Interlocked.Decrement(ref DebugCounters.CallbackHandles);
					}
				}
			}
		}

		internal static FdbFuture<T>? GetFutureFromCallbackParameter(IntPtr parameter)
		{
			if (s_futures.TryGetValue(parameter.ToInt64(), out var future))
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

		protected void RegisterForCancellation(CancellationToken ct)
		{
			//note: if the token is already cancelled, the callback handler will run inline and any exception would bubble up here
			//=> this is not a problem because the ctor already has a try/catch that will clean up everything
			m_ctr = ct.Register(
				(_state) => { CancellationHandler(_state); },
				this,
				false
			);
		}

		protected void UnregisterCancellationRegistration()
		{
			// unsubscribe from the parent cancellation token if there was one
			m_ctr.Dispose();
			m_ctr = default;
		}

		private static void CancellationHandler(object? state)
		{
			if (state is FdbFuture<T> future)
			{
#if DEBUG_FUTURES
				Debug.WriteLine("Future<" + typeof(T).Name + ">.Cancel(0x" + future.m_handle.Handle.ToString("x") + ") was called on thread #" + Environment.CurrentManagedThreadId.ToString());
#endif
				future.Cancel();
			}
		}

		#endregion

		/// <summary>Return true if the future has completed (successfully or not)</summary>
		public bool IsReady => this.Task.IsCompleted;

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
				try
				{
					if (!this.Task.IsCompleted)
					{
						CancelHandles();
						TrySetCanceled();
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
		}

	}

	public sealed class FdbFutureTask<T> : FdbFuture<T>
	{

		public FdbFutureTask(CancellationToken ct)
		{
			if (ct.CanBeCanceled)
			{
				RegisterForCancellation(ct);
			}
		}

		protected override void CloseHandles()
		{
			// NOP
		}

		protected override void CancelHandles()
		{
			// NOP
		}

		protected override void ReleaseMemory()
		{
			// NOP
		}

	}

}
