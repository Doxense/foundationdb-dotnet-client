#undef LOG_SCHEDULING_IN_DB

using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using FoundationDB.Filters.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Layers.Messaging
{

    public class FdbWorkerPool
    {
		/// <summary>Size of the randomly generate id. Range will be 0..(2^(8*N))-1</summary>
		/// <remarks>Should be in the same scale as the hashcode generated from the queue names !</remarks>
		private const int RANDOM_ID_BYTES = 4;

		private readonly RandomNumberGenerator m_rng = RandomNumberGenerator.Create();

		public FdbSubspace Subspace { get; private set; }

		internal FdbSubspace TaskStore { get; private set; }

		internal FdbSubspace IdleRing { get; private set; }

		internal FdbSubspace BusyRing { get; private set; }

		internal FdbSubspace UnassignedTaskRing { get; private set; }

#if LOG_SCHEDULING_IN_DB
		internal FdbSubspace DebugScheduleLog { get; private set; }
		internal FdbSubspace DebugWorkLog { get; private set; }
#endif

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

		public FdbWorkerPool(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;

			this.TaskStore = subspace.Partition(Slice.FromChar('T'));
			this.IdleRing = subspace.Partition(Slice.FromChar('I'));
			this.BusyRing = subspace.Partition(Slice.FromChar('B'));
			this.UnassignedTaskRing = subspace.Partition(Slice.FromChar('U'));

#if LOG_SCHEDULING_IN_DB
			this.DebugScheduleLog = subspace.Partition("Sched");
			this.DebugWorkLog = subspace.Partition("Work");
#endif
		}

		private async Task<KeyValuePair<Slice, Slice>> FindRandomItem(IFdbTransaction tr, FdbSubspace ring)
		{
			var range = ring.ToRange();

			// start from a random position around the ring
			Slice key = ring.Pack(GetRandomId());

			// We want to find the next item in the clockwise direction. If we reach the end of the ring, we "wrap around" by starting again from the start
			// => So we do find_next(key <= x < MAX) and if that does not produce any result, we do a find_next(MIN <= x < key)

			// When the ring only contains a few items (or is empty), there is more than 50% change that we wont find anything in the first read.
			// To reduce the latency for this case, we will issue both range reads at the same time, and discard the second one if the first returned something.
			// This should reduce the latency in half when the ring is empty, or when it contains only items before the random key.

			var candidates = await Task.WhenAll(
				tr.GetRange(key, range.End).FirstOrDefaultAsync(),
				tr.GetRange(range.Begin, key).FirstOrDefaultAsync()
			).ConfigureAwait(false);

			return candidates[0].Key != null ? candidates[0] : candidates[1];
		}

		private Slice GetQueueKey(string queueName)
		{
			//HACKHACK: use the hashcode of the queue name to place it around the ring
			//TODO: string.GetHashcode() is not stable across versions of .NET or plateforms! Replace this with something that will always produce the same results (XxHash? CityHash?)
			return Slice.FromFixed32(queueName.GetHashCode());
		}

		private Slice GetRandomId()
		{
			lock(m_rng)
			{
				return Slice.Random(m_rng, RANDOM_ID_BYTES, nonZeroBytes: false);
			}
		}

		private async Task PushQueueAsync(IFdbTransaction tr, Slice queue, Slice taskId)
		{
			//TODO: use a high contention algo ?
			// - must support Push and Pop
			// - an empty queue must correspond to an empty subspace

			// get the current size of the queue
			var range = FdbTuple.ToRange(queue);
			var lastKey = await tr.Snapshot.GetKeyAsync(FdbKeySelector.LastLessThan(range.End)).ConfigureAwait(false);
			int count = lastKey < range.Begin ? 0 : FdbTuple.UnpackWithoutPrefix(lastKey, queue).Get<int>(0) + 1;

			// set the value
			tr.Set(FdbTuple.Concat(queue, count, GetRandomId()), taskId);
		}

		/// <summary>Add and Schedule a new Task in the worker pool</summary>
		/// <param name="dbOrTrans">Either a database or a transaction</param>
		/// <param name="queueName"></param>
		/// <param name="taskId"></param>
		/// <param name="taskBody"></param>
		/// <param name="ct"></param>
		/// <returns></returns>
		public async Task ScheduleTaskAsync(IFdbTransactional dbOrTrans, string queueName, Slice taskId, Slice taskBody, CancellationToken ct = default(CancellationToken))
		{
			await dbOrTrans.ReadWriteAsync(async (tr) =>
			{
				Interlocked.Increment(ref m_schedulingAttempts);
#if DEBUG
				if (tr.Context.Retries > 0) Console.WriteLine("# retry n°" + tr.Context.Retries + " for task " + taskId.ToAsciiOrHexaString());
#endif
				tr.Annotate("I want to schedule " + taskId.ToAsciiOrHexaString() + " on queue " + queueName);


				// store the task body
				tr.Set(this.TaskStore.Pack(taskId), taskBody);

				// find a random worker from the idle ring
				var randomWorkerKey = await FindRandomItem(tr, this.IdleRing).ConfigureAwait(false);

				if (randomWorkerKey.Key != null)
				{
					Slice workerId = this.IdleRing.UnpackSingle<Slice>(randomWorkerKey.Key);

					tr.Annotate("Assigning " + taskId.ToAsciiOrHexaString() + " to " + workerId.ToAsciiOrHexaString());

					// remove worker from the idle ring
					tr.Clear(this.IdleRing.Pack(workerId));
					// assign task to the worker
					tr.Set(this.BusyRing.Pack(workerId), taskId);
#if LOG_SCHEDULING_IN_DB
					tr.Set(this.DebugScheduleLog.Pack(taskId), Slice.FromString("Assigned to worker at " + index.ToAsciiOrHexaString() + " (#" + tr.Context.Retries + ")"));
#endif
				}
				else
				{
					var queueKey = GetQueueKey(queueName);

					tr.Annotate("Queueing " + taskId.ToAsciiOrHexaString() + " on " + queueKey.ToAsciiOrHexaString());

					await PushQueueAsync(tr, this.UnassignedTaskRing.Pack(queueKey), taskId).ConfigureAwait(false);

#if LOG_SCHEDULING_IN_DB
					tr.Set(this.DebugScheduleLog.Pack(taskId), Slice.FromString("Pushed on queue '" + queueName + "' at " + queueKey.ToAsciiOrHexaString() + " (#" + tr.Context.Retries + ")"));
#endif
				}
			}, 
			onDone: (tr) =>
			{
				Interlocked.Increment(ref m_schedulingMessages);
			},
			cancellationToken: ct).ConfigureAwait(false);
		}

		static int counter = 0;

		/// <summary>Run the worker loop</summary>
		public async Task RunWorkerAsync(IFdbDatabase db, Func<string, Slice, Slice, CancellationToken, Task> handler, CancellationToken ct)
		{
			int num = Interlocked.Increment(ref counter);

			Slice workerId = Slice.Nil;
			Slice taskId = Slice.Nil;
			Slice taskBody = Slice.Nil;
			Slice previousTaskId = Slice.Nil;
			FdbWatch watch = default(FdbWatch);

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
							tr.Annotate("I'm worker #" + num + " with id " + workerId.ToAsciiOrHexaString());

							myId = workerId;
							taskId = Slice.Nil;
							taskBody = Slice.Nil;
							watch = default(FdbWatch);

							if (previousTaskId != null)
							{ // we need to clean up the previous task
								tr.Annotate("Clearing previous task " + previousTaskId.ToAsciiOrHexaString());
								tr.Clear(this.TaskStore.Pack(previousTaskId));
							}
							else if (myId.IsPresent)
							{ // look for an already assigned task
								tr.Annotate("Look for already assigned task");
								taskId = await tr.GetAsync(this.BusyRing.Pack(myId)).ConfigureAwait(false);
							}

							if (!taskId.IsPresent)
							{ // We aren't already assigned a task, so get an item from a random queue

								tr.Annotate("Look for random queued item");
								
								// Find a random queue and peek the first Task in one step
								// > this works because if we find a result it is guranteed to be the first item in the queue
								var key = await FindRandomItem(tr, this.UnassignedTaskRing).ConfigureAwait(false);

								if (key.Key != null)
								{ // pop the Task from the queue
									taskId = key.Value;
									tr.Clear(key.Key);
								}

								if (taskId.IsPresent)
								{ // mark this worker as busy
									// note: we need a random id so generate one if it is the first time...
									if (!myId.IsPresent) myId = GetRandomId();
									tr.Annotate("Found " + taskId + ", switch to busy with id " + myId.ToAsciiOrHexaString());
									tr.Set(this.BusyRing.Pack(myId), taskId);
								}
								else if (myId.IsPresent)
								{ // remove ourselves from the busy ring
									tr.Annotate("Found nothing, switch to idle with id " + myId.ToAsciiOrHexaString());
									//tr.Clear(this.BusyRing.Pack(myId));
								}
							}

							if (taskId.IsPresent)
							{ // get the task body

								tr.Annotate("Fetching body for task " + taskId);
								taskBody = await tr.GetAsync(this.TaskStore.Pack(taskId)).ConfigureAwait(false);
#if LOG_SCHEDULING_IN_DB
								tr.Set(this.DebugWorkLog.Pack(taskId), Slice.FromString("Being processed by worker with id " + myId.ToAsciiOrHexaString()));
#endif
							}
							else
							{ // There are no unassigned task, so enter the idle_worker_ring and wait for a task to be asssigned to us

								// remove us from the busy ring
								tr.Clear(this.BusyRing.Pack(myId));
								tr.AtomicAdd(this.CountersBusy, CounterDecrement);

								// choose a new random position on the idle ring
								myId = GetRandomId();

								// the idle key will also be used as the watch key to wake us up
								var watchKey = this.IdleRing.Pack(myId);
								tr.Annotate("Will start watching on key " + watchKey.ToAsciiOrHexaString() + " with id " + myId.ToAsciiOrHexaString());
								tr.Set(watchKey, Slice.Empty);
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

					if (taskId.IsNullOrEmpty)
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
						previousTaskId = taskId;

						if (taskBody.IsNull)
						{ // the task has been dropped?
							// TODO: loggin?
							Console.WriteLine("[####] Task[" + taskId.ToAsciiOrHexaString() + "] has vanished?");
						}
						else
						{
							try
							{
								await RunTask(db, taskId, taskBody, handler, ct);
							}
							catch (Exception e)
							{
								//TODO: logging?
								Console.Error.WriteLine("Task[" + taskId.ToAsciiOrHexaString() + "] failed: " + e.ToString());
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

		private async Task RunTask(IFdbDatabase db, Slice taskId, Slice taskBody, Func<string, Slice, Slice, CancellationToken, Task> handler, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				Interlocked.Increment(ref m_workerTasksReceived);

				//TODO: custom TaskScheduler for task execution ?
				await handler("Queue??", taskId, taskBody, ct).ConfigureAwait(false);

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
