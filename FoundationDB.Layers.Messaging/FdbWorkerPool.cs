#undef LOG_SCHEDULING_IN_DB

using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

		private async Task<Slice> FindRandomItem(IFdbTransaction tr, FdbSubspace ring)
		{
			var range = ring.ToRange();

			// start from a random position around the ring
			Slice key = ring.Pack(GetRandomId());

			var candidate = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(key)).ConfigureAwait(false);

			if (candidate >= range.End)
			{ // nothing found until the end of the ring, we need to wrap around and look from the start of the ring...

				candidate = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(range.Begin)).ConfigureAwait(false);

				if (candidate >= key)
				{ // we wrapped around without finding anything
					candidate = Slice.Nil;
				}
			}

			return candidate.IsNullOrEmpty ? Slice.Nil : candidate;
		}

		private Slice GetQueueKey(string queueName)
		{
			// use the hashcode of the queue name to place it around the ring
			//TODO: string.GetHashcode() is not stable across versions of .NET or plateforms! Replace this with something that will always produce the same results (XxHash? CityHash?)
			Slice index = Slice.FromFixed32(queueName.GetHashCode());

#if LOGLOG
			Console.WriteLine("['" + queueName + "'] => " + index.ToAsciiOrHexaString());
#endif

			return index;
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
			var lastKey = await tr.ToSnapshotTransaction().GetKeyAsync(FdbKeySelector.LastLessThan(range.End)).ConfigureAwait(false);
			int count = lastKey < range.Begin ? 0 : FdbTuple.UnpackWithoutPrefix(lastKey, queue).Get<int>(0) + 1;

			// set the value
			tr.Set(FdbTuple.Concat(queue, count, GetRandomId()), taskId);
		}

		private async Task<Slice> PopQueueAsync(IFdbTransaction tr, Slice queue)
		{
			var firstItem = await tr.GetRange(FdbTuple.ToRange(queue)).FirstOrDefaultAsync().ConfigureAwait(false);
			if (firstItem.Key.IsNull) return Slice.Nil;

			tr.Clear(firstItem.Key);
			return firstItem.Value;
		}

		public async Task ScheduleTaskAsync(IFdbTransactional dbOrTrans, string queueName, Slice taskId, Slice taskBody, CancellationToken ct = default(CancellationToken))
		{
			await dbOrTrans.ReadWriteAsync(async (tr) =>
			{
				if (tr.Context.Retries > 0) Console.WriteLine("# retry n°" + tr.Context.Retries + " for task " + taskId.ToAsciiOrHexaString());

				// store the task body
				tr.Set(this.TaskStore.Pack(taskId), taskBody);

				// find a random worker from the idle ring
				var key = await FindRandomItem(tr, this.IdleRing).ConfigureAwait(false);

				if (key.IsPresent)
				{
					Slice index = this.IdleRing.UnpackSingle<Slice>(key);
#if LOGLOG
					Console.WriteLine("> [producer] found idle worker at " + index.ToAsciiOrHexaString() + ", assigning task " + taskId.ToAsciiOrHexaString());
#endif

					// remove worker from the idle ring
					tr.Clear(this.IdleRing.Pack(index));
					// assign task to the worker
					tr.Set(this.BusyRing.Pack(index), taskId);
#if LOG_SCHEDULING_IN_DB
					tr.Set(this.DebugScheduleLog.Pack(taskId), Slice.FromString("Assigned to worker at " + index.ToAsciiOrHexaString() + " (#" + tr.Context.Retries + ")"));
#endif
				}
				else
				{
					key = GetQueueKey(queueName);
#if LOGLOG
					Console.WriteLine("> [producer] found no idle workers, pushing task " + taskId.ToAsciiOrHexaString() + " to unassigned queue at " + key.ToAsciiOrHexaString());
#endif
				
					await PushQueueAsync(tr, this.UnassignedTaskRing.Pack(key), taskId);

#if LOG_SCHEDULING_IN_DB
					tr.Set(this.DebugScheduleLog.Pack(taskId), Slice.FromString("Pushed on queue '" + queueName + "' at " + key.ToAsciiOrHexaString() + " (#" + tr.Context.Retries + ")"));
#endif
				}
			}, ct).ConfigureAwait(false);

			Interlocked.Increment(ref m_schedulingMessages);
		}

		/// <summary>Run the worker loop</summary>
		public async Task RunWorkerAsync(FdbDatabase db, Func<string, Slice, Slice, CancellationToken, Task> handler, CancellationToken ct)
		{
			Slice myId = Slice.Nil;
			Slice taskId = Slice.Nil;
			FdbWatch watch = default(FdbWatch);

			Stopwatch idleTimer = Stopwatch.StartNew();

			Interlocked.Increment(ref m_workers);
			try
			{

				while (true)
				{
					ct.ThrowIfCancellationRequested();

					await db.ReadWriteAsync(async (tr) =>
					{
						// look for an already assigned task
						taskId = Slice.Nil;
						if (myId.IsPresent)
						{
							taskId = await tr.GetAsync(this.BusyRing.Pack(myId)).ConfigureAwait(false);
						}

						if (taskId.IsNullOrEmpty)
						{ // We aren't already assigned a task, so get an item from a random queue

							var key = await FindRandomItem(tr, this.UnassignedTaskRing).ConfigureAwait(false);

							if (key.IsPresent)
							{
								// pop the Task from the queue
								taskId = await tr.GetAsync(key).ConfigureAwait(false);
								tr.Clear(key);
							}

							if (taskId.IsPresent)
							{ // mark this worker as busy
								// note: we need a random id so generate one if it is the first time...
								if (!myId.IsPresent) myId = GetRandomId();
								tr.Set(this.BusyRing.Pack(myId), taskId);
							}
							else if (myId.IsPresent)
							{ // remove ourselves from the busy ring
								tr.Clear(this.BusyRing.Pack(myId));
							}
						}

						if (taskId.IsNullOrEmpty)
						{ // There are no unassigned task, so enter the idle_worker_ring and wait for a task to be asssigned to us

							// choose a new random position on the idle ring
							myId = GetRandomId();

							var watchKey = this.IdleRing.Pack(myId);
							tr.Set(watchKey, Slice.Empty);
							watch = tr.Watch(watchKey, ct);
						}

#if LOG_SCHEDULING_IN_DB
						if (taskId.IsPresent)
						{
							tr.Set(this.DebugWorkLog.Pack(taskId), Slice.FromString("Being processed by worker with id " + myId.ToAsciiOrHexaString()));
						}
#endif

					}, ct).ConfigureAwait(false);


					if (taskId.IsPresent)
					{
						idleTimer.Stop();
						try
						{
							await RunTask(db, taskId, handler, ct);
						}
						catch (Exception e)
						{
							Console.Error.WriteLine("Task[" + taskId.ToAsciiOrHexaString() + "] failed: " + e.ToString());
						}
						finally
						{
							idleTimer.Restart();
						}

						// clear the work
						await db.WriteAsync((tr) =>
						{
							tr.Clear(this.BusyRing.Pack(myId));
							tr.Clear(this.TaskStore.Pack(taskId));
#if LOG_SCHEDULING_IN_DB
							tr.Set(this.DebugWorkLog.Pack(taskId), Slice.FromString("Completed by worker with id " + myId.ToAsciiOrHexaString()));
#endif
						}, ct);

					}
					else
					{ // wait for someone to wake us up...
						Interlocked.Increment(ref m_idleWorkers);
						try
						{
							await watch.Task;
						}
						finally
						{
							Interlocked.Decrement(ref m_idleWorkers);
						}
					}
				}
			}
			finally
			{
				Interlocked.Decrement(ref m_workers);
			}
		}

		private async Task RunTask(FdbDatabase db, Slice taskId, Func<string, Slice, Slice, CancellationToken, Task> handler, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				Interlocked.Increment(ref m_workerTasksReceived);

				// get the task body !
				var taskBody = await db.ReadAsync((tr) =>
				{
					if (tr.Context.Retries > 0) Console.WriteLine("#RETRY#");
					return tr.GetAsync(this.TaskStore.Pack(taskId));
				}, ct).ConfigureAwait(false);
				//var taskBody = Slice.FromString("fooooo");

				if (taskBody.IsNull)
				{ // the task has been dropped?
					Console.WriteLine("[####] Task[" + taskId.ToAsciiOrHexaString() + "] has vanished?");
					return;
				}

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


	public class WorkerPoolTest
	{

		public void Main()
		{

			ThreadPool.SetMinThreads(Environment.ProcessorCount, Environment.ProcessorCount);

			Fdb.Start();
			var cts = new CancellationTokenSource();
			try
			{
				string clusterFile = null;
				//string clusterFile = @"c:\temp\fdb\nuc.cluster";
				string dbName = "DB";
				using (var db = Fdb.OpenAsync(clusterFile, dbName).GetAwaiter().GetResult())
				{
					var location = db.Root.CreateOrOpenAsync(FdbTuple.Create("T", "WorkerPool")).GetAwaiter().GetResult();
					db.ClearRangeAsync(location).GetAwaiter().GetResult();

					// failsafe: remove this when not debugging problems !
					cts.CancelAfter(TimeSpan.FromSeconds(60));

					const int N = 100; // msg/publiser
					const int K = 16; // publishers
					const int W = 16; // workers

					RunAsync(db, location, cts.Token, () => cts.Cancel(), N, K, W).GetAwaiter().GetResult();
				};
			}
			catch (TaskCanceledException)
			{
				Console.WriteLine("CANCELED");
			}
			catch(Exception e)
			{
				cts.Cancel();
				Console.Error.WriteLine("CRASH: " + e.ToString());
				Console.Error.WriteLine();
			}
			finally
			{
				Fdb.Stop();
			}
		}

		private async Task RunAsync(FdbDatabase db, FdbSubspace location, CancellationToken ct, Action done, int N, int K, int W)
		{
			if (db == null) throw new ArgumentNullException("db");

			var workerPool = new FdbWorkerPool(location);
			Console.WriteLine("workerPool at " + location.Key.ToAsciiOrHexaString());

			var workerSignal = new TaskCompletionSource<object>();
			var clientSignal = new TaskCompletionSource<object>();

			int taskCounter = 0;

			int msgSent = 0;
			int msgReceived = 0;

			Func<string, Slice, Slice, CancellationToken, Task> handler = async (queue, id, body, _ct) => 
			{
				Interlocked.Increment(ref msgReceived);

				//await Task.Delay(10 + Math.Abs(id.GetHashCode()) % 50);
				await Task.Delay(10);

			};

			Func<int, Task> worker = async (id) =>
			{
				await workerSignal.Task;
				Console.WriteLine("Worker #" + id + " is starting");
				try
				{
					await workerPool.RunWorkerAsync(db, handler, ct);
				}
				finally
				{
					Console.WriteLine("Worker #" + id + " has stopped");
				}
			};

			Func<int, Task> client = async (id) =>
			{
				await clientSignal.Task;
				await Task.Delay(10);

				var rnd = new Random(id * 111);
				for (int i = 0; i < N; i++)
				{
					var taskId = Slice.FromString("T" + Interlocked.Increment(ref taskCounter));
					string queueName = "Q_" + rnd.Next(16).ToString();
					var taskBody = Slice.FromString("Message " + (i + 1) + " of " + N + " from client #" + id + " on queue " + queueName);

					await workerPool.ScheduleTaskAsync(db, queueName, taskId, taskBody, ct);
					Interlocked.Increment(ref msgSent);

					//if (i > 0 && i % 10 == 0) Console.WriteLine("@@@ Client#" + id + " pushed " + (i + 1) + " / " + N + " messages");

					switch(rnd.Next(5))
					{
						case 0: await Task.Delay(10); break;
						case 1: await Task.Delay(100); break;
						case 2: await Task.Delay(500); break;
					}
				}
				Console.WriteLine("@@@ Client#" + id + " has finished!");
			};

			Func<string, Task> dump = async (label) =>
			{
				Console.WriteLine("<dump label='" + label + "' key='" + location.Key.ToAsciiOrHexaString() + "'>");
				using (var tr = db.BeginTransaction(ct))
				{
					await tr.Snapshot
						.GetRange(FdbKeyRange.StartsWith(location.Key))
						.ForEachAsync((kvp) =>
						{
							Console.WriteLine(" - " + FdbTuple.Unpack(location.Extract(kvp.Key)) + " = " + kvp.Value.ToAsciiOrHexaString());
						});
				}
				Console.WriteLine("</dump>");
			};

			var workers = Enumerable.Range(0, W).Select((i) => worker(i)).ToArray();
			var clients = Enumerable.Range(0, K).Select((i) => client(i)).ToArray();

			DateTime start = DateTime.Now;
			DateTime last = start;
			int lastHandled = -1;
			using (var timer = new Timer((_) =>
			{
				var now = DateTime.Now;
				Console.WriteLine("@@@ T=" + now.Subtract(start) + ", sent: " + msgSent.ToString("N0") + ", recv: " + msgReceived.ToString("N0"));
				Console.WriteLine("### Workers: " + workerPool.IdleWorkers + " / " + workerPool.ActiveWorkers + " (" + new string('#', workerPool.IdleWorkers) + new string('.', workerPool.ActiveWorkers - workerPool.IdleWorkers) + "), sent: " + workerPool.MessageScheduled.ToString("N0") + ", recv: " + workerPool.MessageReceived.ToString("N0") + ", delta: " + (workerPool.MessageScheduled - workerPool.MessageReceived).ToString("N0") + ", busy: " + workerPool.WorkerBusyTime + " (avg " + workerPool.WorkerAverageBusyDuration.TotalMilliseconds.ToString("N3") + " ms)");

				if (now.Subtract(last).TotalSeconds >= 10)
				{
					//dump("timer").GetAwaiter().GetResult();
					last = now;
					if (lastHandled == msgReceived)
					{ // STALL ?
						Console.WriteLine("STALL! ");
						done();
					}
					lastHandled = msgReceived;
				}

				if (msgReceived >= K * N)
				{
					dump("complete").GetAwaiter().GetResult();
					done();
				}


			}, null, 1000, 1000))
			{

				var sw = Stopwatch.StartNew();

				// start the workers
				await Task.Run(() => workerSignal.SetResult(null));
				await Task.Delay(500);

				await dump("workers started");

				// start the clients
				await Task.Run(() => clientSignal.SetResult(null));

				await Task.WhenAll(clients);
				Console.WriteLine("Clients completed after " + sw.Elapsed);

				await Task.WhenAll(workers);
				Console.WriteLine("Workers completed after " + sw.Elapsed);
			}
		}


	}

}
