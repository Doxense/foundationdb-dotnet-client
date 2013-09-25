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

		public FdbWorkerPool(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;

			this.TaskStore = subspace.Partition(Slice.FromChar('T'));
			this.IdleRing = subspace.Partition(Slice.FromChar('I'));
			this.BusyRing = subspace.Partition(Slice.FromChar('B'));
			this.UnassignedTaskRing = subspace.Partition(Slice.FromChar('U'));
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

			return candidate.IsNullOrEmpty ? Slice.Nil : ring.Extract(candidate);
		}

		private Slice GetQueueKey(string queueName)
		{
			// use the hashcode of the queue name to place it around the ring
			//TODO: string.GetHashcode() is not stable across versions of .NET or plateforms! Replace this with something that will always produce the same results (XxHash? CityHash?)
			Slice index = Slice.FromFixed32(queueName.GetHashCode());

			Console.WriteLine("[] '" + queueName + "' => " + index.ToAsciiOrHexaString());

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
			tr.Set(FdbTuple.Concat(queue, count), taskId);
		}

		private async Task<Slice> PopQueueAsync(IFdbTransaction tr, Slice queue)
		{
			var firstItem = await tr.GetRange(FdbTuple.ToRange(queue)).FirstOrDefaultAsync().ConfigureAwait(false);
			if (firstItem.Key.IsNull) return Slice.Nil;

			tr.Clear(firstItem.Key);
			return firstItem.Value;
		}

		public Task ScheduleTaskAsync(IFdbTransactional dbOrTrans, string queueName, Slice taskId, Slice taskBody, CancellationToken ct = default(CancellationToken))
		{
			return dbOrTrans.ReadWriteAsync(async (tr) =>
			{
				// store the task body
				tr.Set(this.TaskStore.Pack(taskId), taskBody);

				// find a random worker from the idle ring
				var key = await FindRandomItem(tr, this.IdleRing).ConfigureAwait(false);

				if (key.IsPresent)
				{
					Slice index = FdbTuple.UnpackSingle<Slice>(key);
#if LOGLOG
					Console.WriteLine("> [producer] found idle worker at " + index.ToAsciiOrHexaString() + ", assigning task " + taskId.ToAsciiOrHexaString());
#endif

					// remove worker from the idle ring
					tr.Clear(this.IdleRing.Pack(index));
					// assign task to the worker
					tr.Set(this.BusyRing.Pack(index), taskId);
				}
				else
				{
					key = GetQueueKey(queueName);
#if LOGLOG
					Console.WriteLine("> [producer] found no idle workers, pushing task " + taskId.ToAsciiOrHexaString() + " to unassigned queue at " + key.ToAsciiOrHexaString());
#endif
					await PushQueueAsync(tr, this.UnassignedTaskRing.Pack(key), taskId);
				}
			}, ct);
		}

		/// <summary>Run the worker loop</summary>
		public async Task RunWorkerAsync(FdbDatabase db, CancellationToken ct)
		{
			Slice myId = Slice.Nil;
			Slice taskId = Slice.Nil;
			FdbWatch watch = default(FdbWatch);

			while(true)
			{
				ct.ThrowIfCancellationRequested();

				await db.ReadWriteAsync(async (tr) =>
				{
					// look for an already assigned task
					taskId = Slice.Nil;
					if (myId.IsPresent)
					{
						taskId = await tr.GetAsync(this.BusyRing.Pack(myId)).ConfigureAwait(false);
#if LOGLOG
						if (taskId.IsPresent)
						{
							Console.WriteLine("> [consumer] found work " + taskId.ToAsciiOrHexaString() + " at " + myId.ToAsciiOrHexaString());
						}
						else
						{
							Console.WriteLine("> [consumer] no work assigned to us at " + myId.ToAsciiOrHexaString());
						}
#endif
					}

					if (taskId.IsNullOrEmpty)
					{ // We aren't already assigned as task, so get an item from a random queue

						var key = await FindRandomItem(tr, this.UnassignedTaskRing).ConfigureAwait(false);

						if (key.IsPresent)
						{
							Slice queueIndex = FdbTuple.Unpack(key).Get<Slice>(0);

							taskId = await PopQueueAsync(tr, this.UnassignedTaskRing.Pack(queueIndex)).ConfigureAwait(false);
#if LOGLOG
							Console.WriteLine("Found task " + taskId.ToAsciiOrHexaString() + " on queue " + queueIndex.ToAsciiOrHexaString());
#endif
						}

						if (taskId.IsPresent)
						{
							// note: we need a random id so generate one if it is the first time...
							if (!myId.IsPresent) myId = GetRandomId();
							tr.Set(this.BusyRing.Pack(myId), taskId);
						}
						else if (myId.IsPresent)
						{ // remove ourselves from the busy ring

#if LOGLOG
							Console.WriteLine("> [consumer] we are not busy anymore");
#endif
							tr.Clear(this.BusyRing.Pack(myId));
						}
					}

					if (taskId.IsNullOrEmpty)
					{ // There are no unassigned task, so enter the idle_worker_ring and wait for a task for be asssigned to us
						myId = GetRandomId();
						var watchKey = this.IdleRing.Pack(myId);
						tr.Set(watchKey, Slice.Empty);
						watch = tr.Watch(watchKey, ct);
#if LOGLOG
						Console.WriteLine("> [consumer] going idle on key " + myId.ToAsciiOrHexaString());
#endif
					}

				}, ct).ConfigureAwait(false);
				

				if (taskId.IsPresent)
				{
#if LOGLOG
					Console.WriteLine("> [consumer] got work to do");
#endif
					await RunTask(db, taskId, ct);

					// clear the work
					await db.WriteAsync((tr) =>
					{
						tr.Clear(this.BusyRing.Pack(myId));
						tr.Clear(this.TaskStore.Pack(taskId));
					}, ct);

				}
				else
				{
#if LOGLOG
					Console.WriteLine("> [consumer] waiting for work...");
#endif
					await watch.Task;
#if LOGLOG
					Console.WriteLine("> [consumer] got woken up!");
#endif
				}
			}
		}

		private async Task RunTask(FdbDatabase db, Slice taskId, CancellationToken ct)
		{
			Console.WriteLine("Running task " + taskId.ToAsciiOrHexaString());
			
			// get the task body !
			var taskBody = await db.ReadAsync((tr) => tr.GetAsync(this.TaskStore.Pack(taskId)), ct).ConfigureAwait(false);

			Console.WriteLine("[####] > Task[" + taskId.ToAsciiOrHexaString() + "] : " + taskBody);

			// TODO: work!!!!
			await Task.Delay(10 + (Math.Abs(taskId.GetHashCode()) % 100));
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

				using (var db = Fdb.OpenAsync().GetAwaiter().GetResult())
				{
					var location = db.Root.CreateOrOpenAsync(FdbTuple.Create("T", "WorkerPool")).GetAwaiter().GetResult();
					db.ClearRangeAsync(location).GetAwaiter().GetResult();

					// failsafe: remove this when not debugging problems !
					cts.CancelAfter(TimeSpan.FromSeconds(20));

					RunAsync(db, location, cts.Token).GetAwaiter().GetResult();
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

		private async Task RunAsync(FdbDatabase db, FdbSubspace location, CancellationToken ct)
		{
			const int N = 20;
			const int K = 5;
			const int W = 1;

			var workerPool = new FdbWorkerPool(location);
			Console.WriteLine("workerPool at " + location.Key.ToAsciiOrHexaString());

			var workerSignal = new TaskCompletionSource<object>();
			var clientSignal = new TaskCompletionSource<object>();

			Func<int, Task> worker = async (id) =>
			{
				await workerSignal.Task;
				Console.WriteLine("Worker #" + id + " is starting");
				await workerPool.RunWorkerAsync(db, ct);
				Console.WriteLine("Worker #" + id + " has stopped");
			};

			Func<int, Task> client = async (id) =>
			{
				await clientSignal.Task;
				await Task.Delay(10);

				var rnd = new Random(id * 111);
				for (int i = 0; i < N; i++)
				{
					var taskId = Slice.FromString(Guid.NewGuid().ToString());
					string queueName = "Q_" + new string((char)(65 + rnd.Next(5)), 3);
					var taskBody = Slice.FromString("Message " + (i + 1) + " of " + N + " from client #" + id + " on queue " + queueName);

					await workerPool.ScheduleTaskAsync(db, queueName, taskId, taskBody, ct);

					if (i > 0 && i % 10 == 0) Console.WriteLine("@@@ Client#" + id + " pushed " + (i + 1) + " / " + N + " messages");

					if (rnd.Next(10) < 8)
					{
						await Task.Delay(rnd.Next(15, 100));
					}
				}
				Console.WriteLine("@@@ Client#" + id + " has finished!");
			};

			var workers = Enumerable.Range(0, W).Select((i) => worker(i)).ToArray();
			var clients = Enumerable.Range(0, K).Select((i) => client(i)).ToArray();

			await Task.Run(() => workerSignal.SetResult(null));
			await Task.Delay(500);
			var sw = Stopwatch.StartNew();
			await Task.Run(() => clientSignal.SetResult(null));

			await Task.WhenAll(clients);
			Console.WriteLine("Clients completed after " + sw.Elapsed);

			await Task.WhenAll(workers);
			Console.WriteLine("Workers completed after " + sw.Elapsed);
		}


	}

}
