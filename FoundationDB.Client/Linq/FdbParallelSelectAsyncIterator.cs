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
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Iterates over an async sequence of items, kick off an async task in parallel, and returning the results in order</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class FdbParallelSelectAsyncIterator<TSource, TResult> : FdbAsyncFilter<TSource, TResult>
	{

		// The goal is to have an underlying sequence providing "seed" values (say, documents ids from an ongoing mergesort or intersect)
		// that will kick off tasks for each one (say, a fetch or load for each of the ids) and output the results of these tasks *in order* as they complete
		// Since we can't spin out too many tasks, we also want to be able to put a cap no the max number of pending tasks

		private Func<TSource, CancellationToken, Task<TResult>> m_taskSelector;
		private int m_maxConcurrency;
		private TaskScheduler m_scheduler;

		private readonly object m_lock = new object();
		private CancellationTokenSource m_cts;
		private CancellationToken m_token;
		private volatile bool m_done;
		private volatile bool m_innerBusy;
		private Queue<TaskCompletionSource<KeyValuePair<bool, TResult>>> m_pendingTasks;

		public FdbParallelSelectAsyncIterator(
			IFdbAsyncEnumerable<TSource> source,
			Func<TSource, CancellationToken, Task<TResult>> taskSelector,
			int maxConcurrency,
			TaskScheduler scheduler
		)
			: base(source)
		{
			Contract.Requires(source != null);
			Contract.Requires(taskSelector != null);
			Contract.Requires(maxConcurrency >= 1);

			m_taskSelector = taskSelector;
			m_maxConcurrency = maxConcurrency;
			m_scheduler = scheduler;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbParallelSelectAsyncIterator<TSource, TResult>(m_source, m_taskSelector, m_maxConcurrency, m_scheduler);
		}

		private TaskCompletionSource<KeyValuePair<bool, TResult>> TryGetNextSlot()
		{
			lock (m_lock)
			{
				// not if we know we are done
				if (m_done) return null;

				// not if inner iterator is still busy
				if (m_innerBusy) return null;

				// not if we have reached our max cap
				if (m_pendingTasks.Count >= m_maxConcurrency) return null;

				// we have a slot !
				var tcs = new TaskCompletionSource<KeyValuePair<bool, TResult>>();
				m_pendingTasks.Enqueue(tcs);
				//Console.WriteLine(">> got slot #" + m_pendingTasks.Count);
				return tcs;
			}
		}

		private void FetchNextInnerIfPossible(bool runInline, [System.Runtime.CompilerServices.CallerMemberName] string calledFrom = null)
		{
			var nextSlot = TryGetNextSlot();
			if (nextSlot != null)
			{
				//Console.WriteLine("*** execute slot " + nextSlot.GetHashCode() + " " + (runInline ? "inline" : "deferred") +" ! " + calledFrom);
				if (runInline)
				{
					var _ = PumpInnerAsync(nextSlot);
				}
				else
				{
					var _ = Task.Run(() => PumpInnerAsync(nextSlot));
				}
			}
		}

		/// <summary>Waits for a new item from the inner sequence, spin off the task and update its status when it completes</summary>
		private async Task PumpInnerAsync(TaskCompletionSource<KeyValuePair<bool, TResult>> tcs)
		{
			//Console.WriteLine("> ProcessInnerAsync() called");

			TSource current;

			try
			{
				//Console.WriteLine(" [PumpInnerAsync] " + Thread.CurrentThread.ManagedThreadId);
				while (!m_done && !m_cts.IsCancellationRequested)
				{
					//Console.WriteLine("  Processing " + tcs.GetHashCode() + " ...");
					try
					{
						lock (m_lock)
						{
							Contract.Assert(!m_innerBusy);
							m_innerBusy = true;
						}

						//Console.WriteLine("  [inner.MoveNext()] " + Thread.CurrentThread.ManagedThreadId);

						if (!(await m_iterator.MoveNext(m_token).ConfigureAwait(false)))
						{ // we are done
							//Console.WriteLine("  [/inner.MoveNext()] EOF" + Thread.CurrentThread.ManagedThreadId);
							m_done = true;
							tcs.TrySetResult(new KeyValuePair<bool, TResult>(false, default(TResult)));
							return;
						}

						//Console.WriteLine("  [/inner.MoveNext()] Got Next" + Thread.CurrentThread.ManagedThreadId);

						current = m_iterator.Current;
					}
					finally
					{
						lock (m_lock)
						{
							m_innerBusy = false;
						}
					}

					if (m_token.IsCancellationRequested)
					{
						//Console.WriteLine(">> ct triggered ! => EOF");
						m_done = true;
						tcs.TrySetCanceled();
						return;
					}

					// Spin off the computation of the task in another thread
					var _ = Task.Factory.StartNew(
						async (_state) =>
						{
							var prms = (Tuple<TaskCompletionSource<KeyValuePair<bool, TResult>>, TSource>)_state;
							await ProcessItemAsync(prms.Item1, prms.Item2);
						},
						Tuple.Create(tcs, current),
						m_token,
						TaskCreationOptions.PreferFairness,
						m_scheduler ?? TaskScheduler.Default
					);

					//Console.WriteLine("  looking for another slot...");

					// if there is room in the queue, kick off additionnal work
					tcs = TryGetNextSlot();
					if (tcs == null) break;

					//Console.WriteLine("  tilt!");
				}
			}
			catch (Exception e)
			{
				//Console.WriteLine("> ProcessNextAsync() failed !");
				m_done = true;
				TaskHelpers.PropagateException(tcs, e);
			}
			finally
			{
				//Console.WriteLine(" [/PumpInnerAsync] " + Thread.CurrentThread.ManagedThreadId);
			}
		}

		private async Task ProcessItemAsync(TaskCompletionSource<KeyValuePair<bool, TResult>> tcs, TSource current)
		{
			Contract.Requires(tcs != null);

			// execute a task for an inner item, and signal the TaskCompletionSource when it is done (or failed)

			try
			{
				//Console.WriteLine("  [ProcessItemAsync(" + tcs.GetHashCode() + ", " + current + ")] " + Thread.CurrentThread.ManagedThreadId);

				var result = await m_taskSelector(current, m_token).ConfigureAwait(false);

				tcs.TrySetResult(new KeyValuePair<bool, TResult>(true, result));
			}
			catch (Exception e)
			{
				//Console.WriteLine("> ProcessNextAsync() failed !");
				m_done = true;
				TaskHelpers.PropagateException(tcs, e);
			}
			finally
			{
				//Console.WriteLine("  [/ProcessItemAsync(" + tcs.GetHashCode() + ", " + current + ")] " + Thread.CurrentThread.ManagedThreadId);
			}
		}

		protected override Task<bool> OnFirstAsync(CancellationToken ct)
		{
			//Console.WriteLine("[OnFirstAsync] " + Thread.CurrentThread.ManagedThreadId);
			try
			{
				lock (m_lock)
				{
					m_cts = new CancellationTokenSource();
					m_token = m_cts.Token;
					m_pendingTasks = new Queue<TaskCompletionSource<KeyValuePair<bool, TResult>>>();
					m_innerBusy = false;
					m_done = false;
				}

				return base.OnFirstAsync(ct);
			}
			finally
			{
				//Console.WriteLine("[/OnFirstAsync] " + Thread.CurrentThread.ManagedThreadId);
			}
		}

		protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
		{
			TaskCompletionSource<KeyValuePair<bool, TResult>> tcs = null;
			try
			{
				//Console.WriteLine("[OnNextAsync] " + Thread.CurrentThread.ManagedThreadId);

				lock (m_lock)
				{
					if (m_pendingTasks.Count == 0)
					{
						if (m_done) return Completed();

						FetchNextInnerIfPossible(true);
					}
					Contract.Assert(m_pendingTasks.Count > 0);
					tcs = m_pendingTasks.Peek();
					Contract.Assert(tcs != null);
				}

				// wait for the result
				var result = await tcs.Task.ConfigureAwait(false);

				// we can remove ourselve
				var t = m_pendingTasks.Dequeue();
				Contract.Assert(t == tcs);

				if (!result.Key)
				{ // that was the end of the inner sequence
					return Completed();
				}

				//FetchNextInnerIfPossible(false);

				return Publish(result.Value);
			}
			catch (Exception)
			{
				m_done = true;
				throw;
			}
			finally
			{
				//Console.WriteLine("[/OnNextAsync] " + Thread.CurrentThread.ManagedThreadId);
			}
		}

	}

}
