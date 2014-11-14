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

using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using FoundationDB.Filters.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FoundationDB.Layers.Counters;

namespace FoundationDB.Layers.Messaging
{

	public class FdbWorkerMessage
	{
		public Slice Id { get; internal set; }
		public Slice Body { get; internal set; }
		public DateTimeOffset Scheduled { get; internal set; }
		public DateTimeOffset Received { get; internal set; }
	}

	public class FdbWorkerPool
	{
		/// <summary>Size of the randomly generate id. Range will be 0..(2^(8*N))-1</summary>
		/// <remarks>Should be in the same scale as the hashcode generated from the queue names !</remarks>
		private const int RANDOM_ID_BYTES = 4;

		private const int COUNTER_TOTAL_TASKS = 0;
		private const int COUNTER_IDLE = 1;
		private const int COUNTER_BUSY = 2;
		private const int COUNTER_UNASSIGNED = 3;
		private const int COUNTER_PENDING_TASKS = 4;

		private const int TASK_META_SCHEDULED = 0;

		private readonly RandomNumberGenerator m_rng = RandomNumberGenerator.Create();

		public IFdbSubspace Subspace { get; private set; }

		internal IFdbSubspace TaskStore { get; private set; }

		internal IFdbSubspace IdleRing { get; private set; }

		internal IFdbSubspace BusyRing { get; private set; }

		internal IFdbSubspace UnassignedTaskRing { get; private set; }

		internal FdbCounterMap<int> Counters { get; private set; }

		#region Profiling...

		/// <summary>Number of messages scheduled by this pool</summary>
		private long m_schedulingMessages;
		private long m_schedulingAttempts;
		private long m_schedulingTotalTime;

		/// <summary>Number of task received by a worker of this pool</summary>
		private long m_workerTasksReceived;
		private long m_workerTasksCompleted;
		private long m_workerIdleTime;
		private long m_workerBusyTime;

		/// <summary>Number of local workers active on this pool</summary>
		private int m_workers;

		/// <summary>Number of local workers currently waiting for work</summary>
		private int m_idleWorkers;

		public long MessageScheduled { get { return Volatile.Read(ref m_schedulingMessages); } }

		public long MessageReceived { get { return Volatile.Read(ref m_workerTasksReceived); } }

		public int ActiveWorkers { get { return m_workers; } }

		public int IdleWorkers { get { return m_idleWorkers; } }

		public TimeSpan WorkerBusyTime { get { return TimeSpan.FromTicks(m_workerBusyTime); } }
		public TimeSpan WorkerAverageBusyDuration { get { return m_workerTasksReceived == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(m_workerBusyTime / m_workerTasksReceived); } }

		#endregion

		public FdbWorkerPool(IFdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;

			this.TaskStore = subspace.Partition.By(Slice.FromChar('T'));
			this.IdleRing = subspace.Partition.By(Slice.FromChar('I'));
			this.BusyRing = subspace.Partition.By(Slice.FromChar('B'));
			this.UnassignedTaskRing = subspace.Partition.By(Slice.FromChar('U'));

			this.Counters = new FdbCounterMap<int>(subspace.Partition.By(Slice.FromChar('C')));
		}

		private async Task<KeyValuePair<Slice, Slice>> FindRandomItem(IFdbTransaction tr, IFdbSubspace ring)
		{
			var range = ring.ToRange();

			// start from a random position around the ring
			Slice key = ring.Tuples.EncodeKey(GetRandomId());

			// We want to find the next item in the clockwise direction. If we reach the end of the ring, we "wrap around" by starting again from the start
			// => So we do find_next(key <= x < MAX) and if that does not produce any result, we do a find_next(MIN <= x < key)

			// When the ring only contains a few items (or is empty), there is more than 50% change that we wont find anything in the first read.
			// To reduce the latency for this case, we will issue both range reads at the same time, and discard the second one if the first returned something.
			// This should reduce the latency in half when the ring is empty, or when it contains only items before the random key.

			var candidate = await tr.GetRange(key, range.End).FirstOrDefaultAsync();

			if (!candidate.Key.IsPresent)
			{
				candidate = await tr.GetRange(range.Begin, key).FirstOrDefaultAsync();
			}

			return candidate;
		}

		private Slice GetRandomId()
		{
			lock(m_rng)
			{
				return Slice.Random(m_rng, RANDOM_ID_BYTES, nonZeroBytes: false);
			}
		}

		private async Task PushQueueAsync(IFdbTransaction tr, IFdbSubspace queue, Slice taskId)
		{
			//TODO: use a high contention algo ?
			// - must support Push and Pop
			// - an empty queue must correspond to an empty subspace

			// get the current size of the queue
			var range = queue.ToRange();
			var lastKey = await tr.Snapshot.GetKeyAsync(FdbKeySelector.LastLessThan(range.End)).ConfigureAwait(false);
			int count = lastKey < range.Begin ? 0 : queue.Tuples.DecodeFirst<int>(lastKey) + 1;

			// set the value
			tr.Set(queue.Tuples.EncodeKey(count, GetRandomId()), taskId);
		}

		private void StoreTask(IFdbTransaction tr, Slice taskId, DateTime scheduledUtc, Slice taskBody)
		{
			tr.Annotate("Writing task {0}", taskId.ToAsciiOrHexaString());

			var prefix = this.TaskStore.Partition.By(taskId);

			// store task body and timestamp
			tr.Set(prefix.Key, taskBody);
			tr.Set(prefix.Tuples.EncodeKey(TASK_META_SCHEDULED), Slice.FromInt64(scheduledUtc.Ticks));
			// increment total and pending number of tasks
			this.Counters.Increment(tr, COUNTER_TOTAL_TASKS);
			this.Counters.Increment(tr, COUNTER_PENDING_TASKS);
		}

		private void ClearTask(IFdbTransaction tr, Slice taskId)
		{
			tr.Annotate("Deleting task {0}", taskId.ToAsciiOrHexaString());

			// clear all metadata about the task
			tr.ClearRange(FdbKeyRange.StartsWith(this.TaskStore.Tuples.EncodeKey(taskId)));
			// decrement pending number of tasks
			this.Counters.Decrement(tr, COUNTER_PENDING_TASKS);
		}

		/// <summary>Add and Schedule a new Task in the worker pool</summary>
		/// <param name="db"></param>
		/// <param name="taskId"></param>
		/// <param name="taskBody"></param>
		/// <param name="ct"></param>
		/// <returns></returns>
		public async Task ScheduleTaskAsync(IFdbTransactional db, Slice taskId, Slice taskBody, CancellationToken ct = default(CancellationToken))
		{
			if (db == null) throw new ArgumentNullException("db");
			var now = DateTime.UtcNow;

			await db.ReadWriteAsync(async (tr) =>
			{
				Interlocked.Increment(ref m_schedulingAttempts);
#if DEBUG
				if (tr.Context.Retries > 0) Console.WriteLine("# retry n°" + tr.Context.Retries + " for task " + taskId.ToAsciiOrHexaString());
#endif
				tr.Annotate("I want to schedule {0}", taskId.ToAsciiOrHexaString());

				// find a random worker from the idle ring
				var randomWorkerKey = await FindRandomItem(tr, this.IdleRing).ConfigureAwait(false);

				if (randomWorkerKey.Key != null)
				{
					Slice workerId = this.IdleRing.Tuples.DecodeKey<Slice>(randomWorkerKey.Key);

					tr.Annotate("Assigning {0} to {1}", taskId.ToAsciiOrHexaString(), workerId.ToAsciiOrHexaString());

					// remove worker from the idle ring
					tr.Clear(this.IdleRing.Tuples.EncodeKey(workerId));
					this.Counters.Decrement(tr, COUNTER_IDLE);

					// assign task to the worker
					tr.Set(this.BusyRing.Tuples.EncodeKey(workerId), taskId);
					this.Counters.Increment(tr, COUNTER_BUSY);
				}
				else
				{
					tr.Annotate("Queueing {0}", taskId.ToAsciiOrHexaString());

					await PushQueueAsync(tr, this.UnassignedTaskRing, taskId).ConfigureAwait(false);
				}

				// store the task in the db
				StoreTask(tr, taskId, now, taskBody);
			}, 
			onDone: (tr) =>
			{
				Interlocked.Increment(ref m_schedulingMessages);
			},
			cancellationToken: ct).ConfigureAwait(false);
		}

		static int counter = 0;

		/// <summary>Run the worker loop</summary>
		public async Task RunWorkerAsync(IFdbDatabase db, Func<FdbWorkerMessage, CancellationToken, Task> handler, CancellationToken ct)
		{
			int num = Interlocked.Increment(ref counter);

			Slice workerId = Slice.Nil;
			Slice previousTaskId = Slice.Nil;
			FdbWatch watch = default(FdbWatch);
			FdbWorkerMessage msg = null;

			Interlocked.Increment(ref m_workers);
			try
			{
				while (true)
				{
					//TODO: how do we clear the previousTaskId from the db in case of cancellation ?
					ct.ThrowIfCancellationRequested();

					Slice myId = Slice.Nil;
					await db.ReadWriteAsync(
						async (tr) =>
						{
							tr.Annotate("I'm worker #{0} with id {1}", num, workerId.ToAsciiOrHexaString());

							myId = workerId;
							watch = default(FdbWatch);
							msg = new FdbWorkerMessage();

							if (previousTaskId != null)
							{ // we need to clean up the previous task
								ClearTask(tr, previousTaskId);
							}
							else if (myId.IsPresent)
							{ // look for an already assigned task
								tr.Annotate("Look for already assigned task");
								msg.Id = await tr.GetAsync(this.BusyRing.Tuples.EncodeKey(myId)).ConfigureAwait(false);
							}

							if (!msg.Id.IsPresent)
							{ // We aren't already assigned a task, so get an item from a random queue

								tr.Annotate("Look for next queued item");
								
								// Find the next task on the queue
								var item = await tr.GetRange(this.UnassignedTaskRing.ToRange()).FirstOrDefaultAsync().ConfigureAwait(false);

								if (item.Key != null)
								{ // pop the Task from the queue
									msg.Id = item.Value;
									tr.Clear(item.Key);
								}

								if (msg.Id.IsPresent)
								{ // mark this worker as busy
									// note: we need a random id so generate one if it is the first time...
									if (!myId.IsPresent) myId = GetRandomId();
									tr.Annotate("Found {0}, switch to busy with id {1}", msg.Id.ToAsciiOrHexaString(), myId.ToAsciiOrHexaString());
									tr.Set(this.BusyRing.Tuples.EncodeKey(myId), msg.Id);
									this.Counters.Increment(tr, COUNTER_BUSY);
								}
								else if (myId.IsPresent)
								{ // remove ourselves from the busy ring
									tr.Annotate("Found nothing, switch to idle with id {0}", myId.ToAsciiOrHexaString());
									//tr.Clear(this.BusyRing.Pack(myId));
								}
							}

							if (msg.Id.IsPresent)
							{ // get the task body

								tr.Annotate("Fetching body for task {0}", msg.Id.ToAsciiOrHexaString());
								var prefix = this.TaskStore.Partition.By(msg.Id);
								//TODO: replace this with a get_range ?
								var data = await tr.GetValuesAsync(new [] {
									prefix.ToFoundationDbKey(),
									prefix.Tuples.EncodeKey(TASK_META_SCHEDULED)
								}).ConfigureAwait(false);

								msg.Body = data[0];
								msg.Scheduled = new DateTime(data[1].ToInt64(), DateTimeKind.Utc);
								msg.Received = DateTime.UtcNow;
							}
							else
							{ // There are no unassigned task, so enter the idle_worker_ring and wait for a task to be asssigned to us

								// remove us from the busy ring
								if (myId.IsPresent)
								{
									tr.Clear(this.BusyRing.Tuples.EncodeKey(myId));
									this.Counters.Decrement(tr, COUNTER_BUSY);
								}

								// choose a new random position on the idle ring
								myId = GetRandomId();

								// the idle key will also be used as the watch key to wake us up
								var watchKey = this.IdleRing.Tuples.EncodeKey(myId);
								tr.Annotate("Will start watching on key {0} with id {1}", watchKey.ToAsciiOrHexaString(), myId.ToAsciiOrHexaString());
								tr.Set(watchKey, Slice.Empty);
								this.Counters.Increment(tr, COUNTER_IDLE);

								watch = tr.Watch(watchKey, ct);
							}

						},
						onDone: (tr) =>
						{ // we have successfully acquired some work, or got a watch
							previousTaskId = Slice.Nil;
							workerId = myId;
						},
						cancellationToken: ct
					).ConfigureAwait(false);

					if (msg.Id.IsNullOrEmpty)
					{ // wait for someone to wake us up...
						Interlocked.Increment(ref m_idleWorkers);
						try
						{
							await watch.Task;
							//Console.WriteLine("Worker #" + num + " woken up!");
						}
						finally
						{
							Interlocked.Decrement(ref m_idleWorkers);
						}
					}
					else
					{
						//Console.WriteLine("Got task " + taskId);
						previousTaskId = msg.Id;

						if (msg.Body.IsNull)
						{  // the task has been dropped?
						  // TODO: loggin?
#if DEBUG
							Console.WriteLine("[####] Task[" + msg.Id.ToAsciiOrHexaString() + "] has vanished?");
#endif
						}
						else
						{
							try
							{
								await RunTask(db, msg, handler, ct);
							}
							catch (Exception e)
							{
								//TODO: logging?
#if DEBUG
								Console.Error.WriteLine("Task[" + msg.Id.ToAsciiOrHexaString() + "] failed: " + e.ToString());
#endif
							}
						}
					}
				}
			}
			finally
			{
				//TODO: we should ensure that the last processed task is properly removed from the db before leaving !
				Interlocked.Decrement(ref m_workers);
			}
		}

		private async Task RunTask(IFdbDatabase db, FdbWorkerMessage msg, Func<FdbWorkerMessage, CancellationToken, Task> handler, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				Interlocked.Increment(ref m_workerTasksReceived);

				//TODO: custom TaskScheduler for task execution ?
				await handler(msg, ct).ConfigureAwait(false);

				Interlocked.Increment(ref m_workerTasksCompleted);
			}
			finally
			{
				sw.Stop();
				Interlocked.Add(ref m_workerBusyTime, sw.ElapsedTicks);
			}
		}

	}

}
