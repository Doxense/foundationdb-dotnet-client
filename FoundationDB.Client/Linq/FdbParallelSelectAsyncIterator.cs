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

namespace FoundationDB.Linq
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Iterates over an async sequence of items, kick off an async task in parallel, and returning the results in order</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class FdbParallelSelectAsyncIterator<TSource, TResult> : FdbAsyncFilter<TSource, TResult>
	{
		/// <summary>Default max concurrency when doing batch queries</summary>
		/// <remarks>TODO: this is a placeholder value !</remarks>
		public const int DefaultMaxConcurrency = 32;

		// The goal is to have an underlying sequence providing "seed" values (say, documents ids from an ongoing mergesort or intersect)
		// that will kick off tasks for each one (say, a fetch or load for each of the ids) and output the results of these tasks *in order* as they complete
		// Since we can't spin out too many tasks, we also want to be able to put a cap no the max number of pending tasks

		private Func<TSource, CancellationToken, Task<TResult>> m_taskSelector;
		private FdbParallelQueryOptions m_options;

		private CancellationTokenSource m_cts;
		private CancellationToken m_token;
		private volatile bool m_done;
		private volatile bool m_innerBusy;
		private Queue<TaskCompletionSource<KeyValuePair<bool, TResult>>> m_pendingTasks;
		private int m_maxConcurrency;
		private TaskScheduler m_scheduler;

		/// <summary>Pump that reads values from the inner iterator</summary>
		private FdbAsyncIteratorPump<TSource> m_pump;
		/// <summary>Inner pump task</summary>
		private Task m_pumpTask;
		/// <summary>Queue that holds items that are being processed</summary>
		private AsyncTransformQueue<TSource, TResult> m_processingQueue;

		public FdbParallelSelectAsyncIterator(
			IFdbAsyncEnumerable<TSource> source,
			Func<TSource, CancellationToken, Task<TResult>> taskSelector,
			FdbParallelQueryOptions options
		)
			: base(source)
		{
			Contract.Requires(source != null);
			Contract.Requires(taskSelector != null);
			Contract.Requires(options != null);

			m_taskSelector = taskSelector;
			m_options = options;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbParallelSelectAsyncIterator<TSource, TResult>(m_source, m_taskSelector, m_options);
		}
		protected override async Task<bool> OnFirstAsync(CancellationToken ct)
		{
			if (!await base.OnFirstAsync(ct))
			{
				return false;
			}

			Console.WriteLine("[OnFirstAsync] wiring up inner iterator");

			m_cts = new CancellationTokenSource();
			m_token = m_cts.Token;
			m_done = false;

			// we need a queue to hold the pending tasks (and their results)
			m_processingQueue = new AsyncTransformQueue<TSource, TResult>(m_taskSelector, m_options.MaxConcurrency ?? DefaultMaxConcurrency, m_options.Scheduler);

			// we also need a pump that will work on the inner sequence
			m_pump = new FdbAsyncIteratorPump<TSource>(m_iterator, m_processingQueue);

			// start pumping
			m_pumpTask = m_pump.PumpAsync(m_token);

			Console.WriteLine("[OnFirstAsync] pump started");

			Contract.Ensures(m_pumpTask != null);

			return true;
		}

		protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
		{
			try
			{
				Console.WriteLine("[OnNextAsync] #" + Thread.CurrentThread.ManagedThreadId);

				if (m_done) return false;

				var next = await m_processingQueue.ReceiveAsync(cancellationToken).ConfigureAwait(false);

				if (!next.HasValue)
				{
					m_done = true;
					if (next.HasFailed)
					{
						Console.WriteLine("[OnNextAsync] received failure");
						return Failed(next.Error);
					}
					else
					{
						Console.WriteLine("[OnNextAsync] received completion");
						return Completed();
					}
				}
				Console.WriteLine("[OnNextAsync] received value " + next.Value);

				return Publish(next.Value);
			}
			catch (Exception)
			{
				m_done = true;
				throw;
			}
			finally
			{
				Console.WriteLine("[/OnNextAsync] " + Thread.CurrentThread.ManagedThreadId);
			}
		}

	}

}
