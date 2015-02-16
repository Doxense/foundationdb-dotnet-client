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

namespace FoundationDB.Client.Native
{
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Threading;

	/// <summary>FDBFuture[] wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	internal sealed class FdbFutureArray<T> : FdbFuture<T[]>
	{
		// This future encapsulate multiple FDBFuture* handles and use ref-counting to detect when all the handles have fired
		// The ref-counting is handled by the network thread, and invokation of future.OnReady() is deferred to the ThreadPool once the counter reaches zero
		// The result array is computed once all FDBFuture are ready, from the ThreadPool.
		// If at least one of the FDBFuture fails, the Task fails, using the most "serious" error found (ie: Non-Retryable > Cancelled > Retryable)

		#region Private Members...

		/// <summary>Encapsulated handles</summary>
		// May contains IntPtr.Zero handles if there was a problem when setting up the callbacks.
		// Atomically set to null by the first thread that needs to destroy all the handles
		[CanBeNull]
		private IntPtr[] m_handles;

		/// <summary>Number of handles that haven't fired yet</summary>
		private int m_pending;

		/// <summary>Lambda used to extract the result of one handle</summary>
		// the first argument is the FDBFuture handle that must be ready and not failed
		// the second argument is a state that is passed by the caller.
		[NotNull]
		private readonly Func<IntPtr, object, T> m_resultSelector;

		#endregion

		internal FdbFutureArray([NotNull] IntPtr[] handles, [NotNull] Func<IntPtr, object, T> selector, object state, IntPtr cookie, string label)
			: base(cookie, label, state)
		{
			m_handles = handles;
			m_pending = handles.Length;
			m_resultSelector = selector;
		}

		public override bool Visit(IntPtr handle)
		{
			return 0 == Interlocked.Decrement(ref m_pending);
		}

		public override void OnReady()
		{
			//README:
			// - This callback will fire either from the ThreadPool (async ops) or inline form the ctor of the future (non-async ops, or ops that where served from some cache).
			// - The method *MUST* dispose the future handle before returning, and *SHOULD* do so before signaling the task.
			//   => This is because continuations may run inline, and start new futures from there, while we still have our original future handle opened.

			IntPtr[] handles = null;
			try
			{
				// make sure that nobody can destroy our handles while we are using them.
				handles = Interlocked.Exchange(ref m_handles, null);
				if (handles == null) return; // already disposed?

#if DEBUG_FUTURES
				Debug.WriteLine("FutureArray.{0}<{1}[]>.OnReady([{2}])", this.Label, typeof(T).Name, handles.Length);
#endif

				T[] results = new T[handles.Length];
				FdbError code = FdbError.Success;
				int severity = 0;
				Exception error = null;

				if (this.Task.IsCompleted)
				{ // task has already been handled by someone else
					return;
				}

				var state = this.Task.AsyncState;
				for (int i = 0; i < results.Length; i++)
				{
					var handle = handles[i];
					var err = FdbNative.FutureGetError(handle);
					if (err == FdbError.Success)
					{
						if (code != FdbError.Success)
						{ // there's been at least one error before, so there is no point in computing the result, it would be discarded anyway
							continue;
						}

						try
						{
							results[i] = m_resultSelector(handle, state);
						}
						catch (AccessViolationException e)
						{ // trouble in paradise!

#if DEBUG_FUTURES
							Debug.WriteLine("EPIC FAIL: " + e.ToString());
#endif

							// => THIS IS VERY BAD! We have no choice but to terminate the process immediately, because any new call to any method to the binding may end up freezing the whole process (best case) or sending corrupted data to the cluster (worst case)
							if (Debugger.IsAttached) Debugger.Break();

							Environment.FailFast("FIXME: FDB done goofed!", e);
						}
						catch (Exception e)
						{
#if DEBUG_FUTURES
							Debug.WriteLine("FAIL: " + e.ToString());
#endif
							code = FdbError.InternalError;
							error = e;
							break;
						}
					}
					else if (code != err)
					{
						int cur = FdbFutureContext.ClassifyErrorSeverity(err);
						if (cur > severity)
						{ // error is more serious than before
							severity = cur;
							code = err;
						}
					}
				}

				// since continuations may fire inline, make sure to release all the memory used by this handle first
				FdbFutureContext.DestroyHandles(ref handles);

				if (code == FdbError.Success)
				{
					PublishResult(results);
				}
				else
				{
					PublishError(error, code);
				}
			}
			catch (Exception e)
			{ // we must not blow up the TP or the parent, so make sure to propagate all exceptions to the task
				TrySetException(e);
			}
			finally
			{
				if (handles != null) FdbFutureContext.DestroyHandles(ref handles);
				GC.KeepAlive(this);
			}
		}

		protected override void OnCancel()
		{
			var handles = Volatile.Read(ref m_handles);
			//TODO: we probably need locking to prevent concurrent destroy and cancel calls
			if (handles != null)
			{
				foreach (var handle in handles)
				{
					if (handle != IntPtr.Zero)
					{
						FdbNative.FutureCancel(handle);
					}
				}
			}
		}

	}

}