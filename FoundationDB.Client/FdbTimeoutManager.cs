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
#if false // DOES NOT WORK

using System;
using FoundationDB.Client.Native;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace FoundationDB.Client
{

	/// <summary>Class that handles all timeouts for async operations</summary>
	internal class FdbTimeoutManager
	{
		// Ovservations:
		// * In general, the set values differnt timeout delays used by an application is relatively small. The Connect() to the cluster will use one value, the transaction commit will use another, and so on.
		//   > If we filter all callback into one queue per timeout value, we only need to have to add a new callback at the end of the queue, and query the head of each queues to know the next event that will fire.
		// * Most thread will perform operations one by one usually with the same timeout. The operation will either complete quickly or timeout, before the next operation is issued.
		//   > This means that the queues will often go from empty to one item schedule, to empty again.
		//   > With N threads, the queue will go from 0 to N items. 
		// * When a timeout occurs, it is probable that all items in a queue will expire.

		// To amortize the cost inserting/fetching from the queues, each operation will cleanup the last (for inserting) or first (for checking) item in the queue
		//
		// Insert(Queue, Callback) {
		//   lock(Queue) {
		//     if Queue.NonEmpty and Queue.Last.IsDead then Queue.RemoveLast
		//     Queue.Enqueue(Callback)
		//   }
		// }
		//
		// PeekNext(Queue) {
		//   lock(Queue) {
		//     while(Queue.NonEmpty) {
		//       Next = Queue.Peek
		//       if not Next.IsDead then return Next
		//       Queue.Dequeue
		//     }
		//   }
		//   return null
		// }

		// ExecuteCallback(Callback) {
		//   _atomically_{
		//      if (!Callback.Scheduled) exit
		//      Callback.Scheduled = false
		//    }
		//  try { Callback.Invoke(); } catch { ...}
		// }

		private Thread m_thread;
		private bool m_alive;
		private readonly object m_lock = new object();
		private readonly Dictionary<int, Queue<TimeoutRegistration>> m_timeoutQueues = new Dictionary<int, Queue<TimeoutRegistration>>();
		private readonly ManualResetEventSlim m_mre = new ManualResetEventSlim();

		private Func<DateTime> m_clockUtc = () => DateTime.UtcNow;

		private const int Hertz = 10;
		private const long TicksPerCycle = TimeSpan.TicksPerSecond / Hertz;

		public class TimeoutRegistration : IDisposable
		{
			// If true, the timeout is still scheduled, if false it has already been cancelled or has fired
			public bool Scheduled;
			public DateTime Expires;
			public Action Callback;		
		}

		public FdbTimeoutManager()
		{

		}

		private long UtcNowTicks
		{
			get { return m_clockUtc().Ticks; }
		}

		/// <summary>Return the expiration date from a specified timeout, rounded to the next cycle</summary>
		/// <param name="timeout">Timeout value</param>
		/// <returns>DateTime (UTC!) of the first cycle following the expiration of the value</returns>
		public DateTime ComputeExpirationDate(int timeout)
		{
			long ts = this.UtcNowTicks + (timeout * TimeSpan.TicksPerMillisecond);
			long rem = ts % TicksPerCycle;
			if (rem != 0) ts += TicksPerCycle - rem;
			return new DateTime(ts, DateTimeKind.Utc);
		}

		private Queue<TimeoutRegistration> GetOrCreateTimeoutQueue(int timeout)
		{
			lock (m_lock)
			{
				Queue<TimeoutRegistration> queue;
				if (!m_timeoutQueues.TryGetValue(timeout, out queue))
				{
					queue = new Queue<TimeoutRegistration>();
					m_timeoutQueues.Add(timeout, queue);
				}
				return queue;
			}
		}

		public TimeoutRegistration Schedule(int timeout, Action callback)
		{
			var expires = ComputeExpirationDate(timeout);
			var queue = GetOrCreateTimeoutQueue(timeout);

			var reg = new TimeoutRegistration();
			reg.Expires = expires;
			reg.Callback = callback;

			lock (queue)
			{
				queue.Enqueue(reg);
				reg.Scheduled = true;
				
			}
		}

		/// <summary>Returns the next non-expired event, or null if all queues are empty</summary>
		/// <remarks>Remove stale entries at the start of each queues</remarks>
		private TimeoutRegistration PeekNextEvent()
		{
			lock (m_lock)
			{
				TimeoutRegistration next = null;
				foreach (var queue in m_timeoutQueues.Values)
				{
					lock (queue)
					{
						while (queue.Count > 0)
						{
							var first = queue.Peek();
							if (!first.Scheduled)
							{ // clean up
								queue.Dequeue();
								continue;
							}

							if (next == null || first.Expires < next.Expires) next = first;
						}
					}
				}
				return next;
			}
		}

		public void Start()
		{
			if (m_thread != null)
			{
				lock (m_lock)
				{
					if (m_thread != null)
					{
						Thread thread = null;
						try
						{
							thread = new Thread(new ThreadStart(this.Run));
							thread.IsBackground = true;
							thread.Name = "FdbTimeoutMgr";
							thread.CurrentCulture = CultureInfo.InvariantCulture;
							thread.CurrentUICulture = CultureInfo.InvariantCulture;

							m_alive = true;
							thread.Start();
							m_thread = thread;
						}
						catch (Exception)
						{
							m_thread = null;
							if (thread != null && thread.ThreadState != System.Threading.ThreadState.Unstarted)
							{ // we must cleanup the thread
								thread.Abort();
								//let it die by itself...
							}
						}

					}
				}
			}
		}

		private void FireCallback(TimeoutRegistration reg)
		{
			if (!reg.Scheduled) return;
			//TODO: locking

		}

		private void Run()
		{
			TimeSpan nextEventDelay = TimeSpan.MaxValue;

			while (m_alive)
			{
				if (nextEventDelay == TimeSpan.MaxValue)
					m_mre.Wait();
				else
					m_mre.Wait(nextEventDelay);

				if (!m_alive) break;

				TimeoutRegistration next;
				while ((next = PeekNextEvent()) != null)
				{
					FireCallback(next);
				}
			}
		}

	}

}
#endif
