using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Layers.Messaging
{


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
					const int K = 2; // publishers
					const int W = 2; // workers

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

		private async Task RunAsync(IFdbDatabase db, FdbSubspace location, CancellationToken ct, Action done, int N, int K, int W)
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
