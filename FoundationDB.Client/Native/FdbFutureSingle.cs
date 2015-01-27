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

#undef DEBUG_FUTURES

using System.Diagnostics;

namespace FoundationDB.Client.Native
{
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;

	/// <summary>FDBFuture wrapper</summary>
	/// <typeparam name="T">Type of result</typeparam>
	internal sealed class FdbFutureSingle<T> : FdbFuture<T>
	{
		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private IntPtr m_handle;

		/// <summary>Lambda used to extract the result of this FDBFuture</summary>
		private readonly Func<IntPtr, object, T> m_resultSelector;

		private readonly object m_state;

		#endregion

		internal FdbFutureSingle(IntPtr handle, [NotNull] Func<IntPtr, object, T> selector, object state, IntPtr cookie, string label)
			: base(cookie, label)
		{
			if (handle == IntPtr.Zero) throw new ArgumentException("Invalid future handle", "handle");
			if (selector == null) throw new ArgumentNullException("selector");

			m_handle = handle;
			m_resultSelector = selector;
			m_state = state;
		}

		public override bool Visit(IntPtr handle)
		{
			Contract.Requires(handle == m_handle);
			return true;
		}

		[HandleProcessCorruptedStateExceptions] // to be able to handle Access Violations and terminate the process
		public override void OnFired()
		{
			Debug.WriteLine("Future{0}<{1}>.OnFired(0x{2})", this.Label, typeof(T).Name, m_handle.ToString("X8"));

			var handle = Interlocked.Exchange(ref m_handle, IntPtr.Zero);
			if (handle == IntPtr.Zero) return; // already disposed?

			//README:
			// - This callback will fire either from the ThreadPool (async ops) or inline form the ctor of the future (non-async ops, or ops that where served from some cache).
			// - The method *MUST* dispose the future handle before returning, and *SHOULD* do so before signaling the task.
			//   => This is because continuations may run inline, and start new futures from there, while we still have our original future handle opened.

			try
			{
				T result = default(T);
				FdbError code;
				Exception error = null;
				try
				{
					if (this.Task.IsCompleted)
					{ // task has already been handled by someone else
						return;
					}

					code = FdbNative.FutureGetError(handle);
					if (code == FdbError.Success)
					{
						try
						{
							result = m_resultSelector(handle, m_state);
						}
						catch (AccessViolationException e)
						{ // trouble in paradise!

							Debug.WriteLine("EPIC FAIL: " + e.ToString());

							// => THIS IS VERY BAD! We have no choice but to terminate the process immediately, because any new call to any method to the binding may end up freezing the whole process (best case) or sending corrupted data to the cluster (worst case)
							if (Debugger.IsAttached) Debugger.Break();

							Environment.FailFast("FIXME: FDB done goofed!", e);
						}
						catch (Exception e)
						{
							Debug.WriteLine("FAIL: " + e.ToString());
							code = FdbError.InternalError;
							error = e;
						}
					}
				}
				finally
				{
					FdbNative.FutureDestroy(handle);				
				}

				if (code == FdbError.Success)
				{
					TrySetResult(result);
				}
				else if (code == FdbError.OperationCancelled || code == FdbError.TransactionCancelled)
				{
					TrySetCanceled();
				}
				else
				{
					TrySetException(error ?? Fdb.MapToException(code));
				}
			}
			catch (Exception e)
			{ // we must not blow up the TP or the parent, so make sure to propagate all exceptions to the task
				TrySetException(e);
			}
		}

	}

}
