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

#undef FULL_DEBUG

namespace FoundationDB.Async
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Diagnostics;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Pumps item from a source, and into a target</summary>
	public class AsyncPump<T> : IAsyncPump<T>
	{
		private const int STATE_IDLE = 0;
		private const int STATE_WAITING_FOR_NEXT = 1;
		private const int STATE_PUBLISHING_TO_TARGET = 2;
		private const int STATE_FAILED = 3;
		private const int STATE_DONE = 4;
		private const int STATE_DISPOSED = 5;

		private volatile int m_state;
		private readonly IAsyncSource<T> m_source;
		private readonly IAsyncTarget<T> m_target;

		private ExceptionDispatchInfo m_error;

		public AsyncPump(
			IAsyncSource<T> source,
			IAsyncTarget<T> target
		)
		{
			Contract.Requires(source!= null);
			Contract.Requires(target != null);

			m_source = source;
			m_target = target;
		}

		/// <summary>Returns true if the pump has completed (with success or failure)</summary>
		public bool IsCompleted
		{
			get { return m_state >= STATE_FAILED; }
		}

		internal int State
		{
			get { return m_state; }
		}

		public IAsyncSource<T> Source { get { return m_source; } }

		public IAsyncTarget<T> Target { get { return m_target; } }

		/// <summary>Run the pump until the inner iterator is done, an error occurs, or the cancellation token is fired</summary>
		public async Task PumpAsync(bool stopOnFirstError, CancellationToken cancellationToken)
		{
			if (m_state != STATE_IDLE)
			{
				// either way, we need to stop !
				Exception error;

				if (m_state == STATE_DISPOSED)
				{
					error = new ObjectDisposedException(null, "Pump has already been disposed");
				}
				else if (m_state >= STATE_FAILED)
				{
					error = new InvalidOperationException("Pump has already completed once");
				}
				else
				{
					error = new InvalidOperationException("Pump is already running");
				}

				try
				{
					m_target.OnError(ExceptionDispatchInfo.Capture(error));
				}
				catch
				{
					m_target.OnCompleted();
				}

				throw error;
			}

			try
			{
				LogPump("Starting pump");

				while (!cancellationToken.IsCancellationRequested && m_state != STATE_DISPOSED)
				{
					LogPump("Waiting for next");
					m_state = STATE_WAITING_FOR_NEXT;
					var current = await m_source.ReceiveAsync(cancellationToken).ConfigureAwait(false);

					LogPump("Received " + (current.HasValue ? "value" : current.HasFailed ? "error" : "completion") + ", publishing...");
					m_state = STATE_PUBLISHING_TO_TARGET;

					await m_target.Publish(current, cancellationToken).ConfigureAwait(false);

					if (current.HasFailed && stopOnFirstError)
					{
						m_state = STATE_FAILED;
						LogPump("Stopping after this error");
						current.ThrowIfFailed();
					}
					else if (current.IsEmpty)
					{
						m_state = STATE_DONE;
						LogPump("Completed");
						return;
					}
				}

				// push the cancellation on the queue, and throw
				throw new OperationCanceledException(cancellationToken);
			}
			catch (Exception e)
			{
				LogPump("Failed " + e.Message);

				switch (m_state)
				{
					case STATE_WAITING_FOR_NEXT:
					{ // push the info to the called
						try
						{
							m_target.OnError(ExceptionDispatchInfo.Capture(e));
						}
						catch(Exception x)
						{
							LogPump("Failed to notify target of error: " + x.Message);
							throw;
						}
						break;
					}
					case STATE_PUBLISHING_TO_TARGET: // the error comes from the target itself, push back to caller!
					case STATE_FAILED: // we want to notify the caller of some problem
					{
						throw;
					}
				}
			}
			finally
			{
				if (m_state != STATE_DISPOSED)
				{
					m_target.OnCompleted();
				}
				LogPump("Stopped pump");
			}
		}

		public void Dispose()
		{
			m_state = STATE_DISPOSED;
			GC.SuppressFinalize(this);
		}

		#region Debugging...

		[Conditional("FULL_DEBUG")]
		private static void LogPump(string msg)
		{
			Console.WriteLine("[pump#" + Thread.CurrentThread.ManagedThreadId + "] " + msg);
		}

		#endregion
	}

}
