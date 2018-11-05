#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Async
{
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Pump that takes items from a source, transform them, and outputs them</summary>
	public sealed class AsyncTransform<TInput, TOutput> : IAsyncTarget<TInput>, IDisposable
	{
		private readonly IAsyncTarget<Task<TOutput>> m_target;
		private readonly Func<TInput, CancellationToken, Task<TOutput>> m_transform;
		private readonly TaskScheduler m_scheduler;
		private bool m_done;

		public AsyncTransform([NotNull] Func<TInput, CancellationToken, Task<TOutput>> transform, [NotNull] IAsyncTarget<Task<TOutput>> target, TaskScheduler scheduler = null)
		{
			Contract.NotNull(transform, nameof(transform));
			Contract.NotNull(target, nameof(target));

			m_transform = transform;
			m_target = target;
			m_scheduler = scheduler;
		}

		/// <summary>Target of the transform</summary>
		public IAsyncTarget<Task<TOutput>> Target => m_target;

		/// <summary>Optional scheduler used to run the tasks</summary>
		public TaskScheduler Scheduler => m_scheduler;

		#region IAsyncTarget<T>...

		public Task OnNextAsync(TInput value, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.CompletedTask;

			if (m_done) throw new InvalidOperationException("Cannot send any more values because this transform has already completed.");

			try
			{

				// we start the task here, but do NOT wait for its completion!
				// It is the job of the target to handle that (and ordering)
				Task<TOutput> task;
				if (m_scheduler == null)
				{ // execute inline
					task = m_transform(value, ct);
				}
				else
				{ // execute in a scheduler
					task = Task.Factory.StartNew(
						(state) =>
						{
							var prms = (Tuple<AsyncTransform<TInput, TOutput>, TInput, CancellationToken>)state;
							return prms.Item1.m_transform(prms.Item2, prms.Item3);
						},
						Tuple.Create(this, value, ct),
						ct,
						TaskCreationOptions.PreferFairness,
						m_scheduler
					).Unwrap();
				}

				return m_target.OnNextAsync(task, ct);
			}
			catch(Exception e)
			{
				m_target.OnError(ExceptionDispatchInfo.Capture(e));
				return Task.FromException<object>(e);
			}
		}

		public void OnCompleted()
		{
			Dispose();
		}

		public void OnError(ExceptionDispatchInfo e)
		{
			if (!m_done)
			{
				m_target.OnError(e);
			}
		}

		#endregion

		#region IDisposable...

		public void Dispose()
		{
			if (!m_done)
			{
				m_done = true;
				m_target.OnCompleted();
			}
		}

		#endregion

	}

}

#endif
