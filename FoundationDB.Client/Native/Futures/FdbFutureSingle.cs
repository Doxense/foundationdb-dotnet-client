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
	using System;
	using System.Diagnostics;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>FDBFuture wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	internal sealed class FdbFutureSingle<T> : FdbFuture<T>
	{
		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private IntPtr m_handle;

		/// <summary>Lambda used to extract the result of this FDBFuture</summary>
		private readonly Func<IntPtr, object, T> m_resultSelector;

		#endregion

		internal FdbFutureSingle(IntPtr handle, [NotNull] Func<IntPtr, object, T> selector, object state, IntPtr cookie, string label)
			: base(cookie, label, state)
		{
			if (handle == IntPtr.Zero) throw new ArgumentException("Invalid future handle", nameof(handle));
			if (selector == null) throw new ArgumentNullException(nameof(selector));

			m_handle = handle;
			m_resultSelector = selector;
		}

		public override bool Visit(IntPtr handle)
		{
#if DEBUG_FUTURES
			Debug.WriteLine("FutureSingle.{0}<{1}>.Visit(0x{2})", this.Label, typeof(T).Name, handle.ToString("X8"));
#endif
			Contract.Requires(handle == m_handle, this.Label);
			return true;
		}

		[HandleProcessCorruptedStateExceptions] // to be able to handle Access Violations and terminate the process
		public override void OnReady()
		{
			IntPtr handle = IntPtr.Zero;

			//README:
			// - This callback will fire either from the ThreadPool (async ops) or inline form the ctor of the future (non-async ops, or ops that where served from some cache).
			// - The method *MUST* dispose the future handle before returning, and *SHOULD* do so before signaling the task.
			//   => This is because continuations may run inline, and start new futures from there, while we still have our original future handle opened.

			try
			{
				handle = Interlocked.Exchange(ref m_handle, IntPtr.Zero);
				if (handle == IntPtr.Zero) return; // already disposed?

#if DEBUG_FUTURES
				Debug.WriteLine("FutureSingle.{0}<{1}>.OnReady(0x{2})", this.Label, typeof(T).Name, handle.ToString("X8"));
#endif

				if (this.Task.IsCompleted)
				{ // task has already been handled by someone else
					return;
				}

				var result = default(T);
				var error = default(Exception);

				var code = FdbNative.FutureGetError(handle);
				if (code == FdbError.Success)
				{
					try
					{
						result = m_resultSelector(handle, this.Task.AsyncState);
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
					}
				}

				// since continuations may fire inline, make sure to release all the memory used by this handle first
				FdbFutureContext.DestroyHandle(ref handle);

				if (code == FdbError.Success)
				{
					PublishResult(result);
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
				if (handle != IntPtr.Zero) FdbFutureContext.DestroyHandle(ref handle);
				GC.KeepAlive(this);
			}
		}

		protected override void OnCancel()
		{
			IntPtr handle = Volatile.Read(ref m_handle);
			//TODO: we probably need locking to prevent concurrent destroy and cancel calls
			if (handle != IntPtr.Zero) FdbNative.FutureCancel(handle);
		}

	}

}
