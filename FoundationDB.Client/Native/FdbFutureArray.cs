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

		#region Private Members...

		private IntPtr[] m_handles;

		private int m_pending;

		private readonly Func<IntPtr, object, T> m_resultSelector;

		private readonly object m_state;

		#endregion

		internal FdbFutureArray([NotNull] IntPtr[] handles, [NotNull] Func<IntPtr, object, T> selector, object state, IntPtr cookie, string label)
			: base(cookie, label)
		{
			m_handles = handles;
			m_pending = handles.Length;
			m_resultSelector = selector;
			m_state = state;
		}

		public override bool Visit(IntPtr handle)
		{
			return 0 == Interlocked.Decrement(ref m_pending);
		}

		private const int CATEGORY_SUCCESS = 0;
		private const int CATEGORY_RETRYABLE = 1;
		private const int CATEGORY_CANCELLED = 2;
		private const int CATEGORY_FAILURE = 3;

		private static int ClassifyErrorSeverity(FdbError error)
		{
			switch (error)
			{
				case FdbError.Success:
					return CATEGORY_SUCCESS;

				case FdbError.PastVersion:
				case FdbError.FutureVersion:
				case FdbError.TimedOut:
				case FdbError.TooManyWatches:
					return CATEGORY_RETRYABLE;

				case FdbError.OperationCancelled:
				case FdbError.TransactionCancelled:
					return CATEGORY_CANCELLED;

				default:
					return CATEGORY_FAILURE;
			}
		}

		public override void OnFired()
		{
			var handles = Interlocked.Exchange(ref m_handles, null);
			if (handles == null) return; // already disposed?

			Debug.WriteLine("Future{0}<{1}[]>.OnFired({2})", this.Label, typeof (T).Name, handles.Length);

			//README:
			// - This callback will fire either from the ThreadPool (async ops) or inline form the ctor of the future (non-async ops, or ops that where served from some cache).
			// - The method *MUST* dispose the future handle before returning, and *SHOULD* do so before signaling the task.
			//   => This is because continuations may run inline, and start new futures from there, while we still have our original future handle opened.

			try
			{
				T[] results = new T[handles.Length];
				FdbError code = FdbError.Success;
				int severity = 0;
				Exception error = null;
				try
				{
					if (this.Task.IsCompleted)
					{ // task has already been handled by someone else
						return;
					}

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
								results[i] = m_resultSelector(handle, m_state);
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
								break;
							}
						}
						else if (code != err)
						{
							int cur = ClassifyErrorSeverity(err);
							if (cur > severity)
							{ // error is more serious than before
								severity = cur;
								code = err;
							}
						}
					}
				}
				finally
				{
					foreach (var handle in handles)
					{
						if (handle != IntPtr.Zero) FdbNative.FutureDestroy(handle);
					}
				}

				if (code == FdbError.Success)
				{
					TrySetResult(results);
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