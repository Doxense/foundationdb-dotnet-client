//TODO: License for samples/tutorials ???

namespace FoundationDB.Samples.Tutorials
{
	using Doxense.Mathematics.Statistics;
	using FoundationDB.Client;
	using FoundationDB.Layers.Messaging;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	public class MessageQueueRunner : IAsyncTest
	{

		public enum AgentRole
		{
			Producer,
			Worker,
			Clear,
			Status
		}

		public MessageQueueRunner(string id, AgentRole role, TimeSpan delayMin, TimeSpan delayMax)
		{
			this.Id = id;
			this.Role = role;
			this.DelayMin = delayMin;
			this.DelayMax = delayMax;
			this.TimeLine = new RobustTimeLine(
				TimeSpan.FromSeconds(5),
				onCompleted: (histo, idx) =>
				{
					Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0,3} | {1} | {2,6:#,##0.0} ms (+/- {3:#0.0})", idx, histo.GetDistribution(1, 5000 - 1), histo.Median, histo.MedianAbsoluteDeviation()));
					if (idx % 30 == 29)
					{
						Console.WriteLine(this.TimeLine.MergeResults().GetReport(true));
						return true;
					}
					return false;
				}
			);
		}

		public string Id { get; private set; }
		public AgentRole Role { get; private set; }
		public TimeSpan DelayMin { get; private set; }
		public TimeSpan DelayMax { get; private set; }

		public FdbSubspace Subspace { get; private set; }

		public FdbWorkerPool WorkerPool { get; private set; }

		public RobustTimeLine TimeLine { get; private set; }

		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(IFdbDatabase db, CancellationToken ct)
		{
			// open the folder where we will store everything
			this.Subspace = await db.Directory.CreateOrOpenAsync(new [] { "Samples", "MessageQueueTest" }, cancellationToken: ct);

			this.WorkerPool = new FdbWorkerPool(this.Subspace);

		}

		/// <summary>
		/// Simulate a student that is really indecisive
		/// </summary>
		public async Task RunProducer(IFdbDatabase db, CancellationToken ct)
		{
			int cnt = 0;

			var rnd = new Random(123456);

			DateTime last = DateTime.Now;

			rnd = new Random();
			this.TimeLine.Start();
			while (!ct.IsCancellationRequested)
			{
				int k = cnt++;
				Slice taskId = FdbTuple.EncodeKey(this.Id.GetHashCode(), k);

				var ts = Stopwatch.GetTimestamp();
				string msg = "Message #" + k + " from producer " + this.Id + " (" + DateTime.UtcNow.ToString("O") + ")";

				var latency = Stopwatch.StartNew();
				await this.WorkerPool.ScheduleTaskAsync(db, taskId, Slice.FromString(msg), ct).ConfigureAwait(false);
				latency.Stop();
				Console.Write(k.ToString("N0") + "\r");

				this.TimeLine.Add(latency.Elapsed.TotalMilliseconds);

				TimeSpan delay = TimeSpan.FromTicks(rnd.Next((int)this.DelayMin.Ticks, (int)this.DelayMax.Ticks));
				await Task.Delay(delay).ConfigureAwait(false);
			}
			this.TimeLine.Stop();

			ct.ThrowIfCancellationRequested();

		}

		public async Task RunConsumer(IFdbDatabase db, CancellationToken ct)
		{
			var rnd = new Random();

			DateTime last = DateTime.Now;
			int received = 0;

			this.TimeLine.Start();
			await this.WorkerPool.RunWorkerAsync(db, async (msg, _ct) =>
			{
				long ts = Stopwatch.GetTimestamp();

				var latency = msg.Received - msg.Scheduled;
				Interlocked.Increment(ref received);

				Console.Write("[" + received.ToString("N0") + " msg, ~" + latency.TotalMilliseconds.ToString("N3") + " ms] " + msg.Id.ToAsciiOrHexaString() + "            \r");

				this.TimeLine.Add(latency.TotalMilliseconds);

				//Console.Write(".");
				TimeSpan delay = TimeSpan.FromTicks(rnd.Next((int)this.DelayMin.Ticks, (int)this.DelayMax.Ticks));
				await Task.Delay(delay).ConfigureAwait(false);
			}, ct);
			this.TimeLine.Stop();

		}

		public async Task RunClear(IFdbDatabase db, CancellationToken ct)
		{
			// clear everything
			await db.ClearRangeAsync(this.WorkerPool.Subspace, ct);
		}

		public async Task RunStatus(IFdbDatabase db, CancellationToken ct)
		{
			var countersLocation = this.WorkerPool.Subspace.Partition.ByKey(Slice.FromChar('C'));
			var idleLocation = this.WorkerPool.Subspace.Partition.ByKey(Slice.FromChar('I'));
			var busyLocation = this.WorkerPool.Subspace.Partition.ByKey(Slice.FromChar('B'));
			var tasksLocation = this.WorkerPool.Subspace.Partition.ByKey(Slice.FromChar('T'));
			var unassignedLocation = this.WorkerPool.Subspace.Partition.ByKey(Slice.FromChar('U'));

			using(var tr = db.BeginTransaction(ct))
			{
				var counters = await tr.Snapshot.GetRange(countersLocation.Keys.ToRange()).Select(kvp => new KeyValuePair<string, long>(countersLocation.Keys.DecodeLast<string>(kvp.Key), kvp.Value.ToInt64())).ToListAsync().ConfigureAwait(false);

				Console.WriteLine("Status at " + DateTimeOffset.Now.ToString("O"));
				foreach(var counter in counters)
				{
					Console.WriteLine(" - " + counter.Key + " = " + counter.Value);
				}

				Console.WriteLine("Dump:");
				Console.WriteLine("> Idle");
				await tr.Snapshot.GetRange(idleLocation.Keys.ToRange()).ForEachAsync((kvp) =>
				{
					Console.WriteLine("- Idle." + idleLocation.Keys.Unpack(kvp.Key) + " = " + kvp.Value.ToAsciiOrHexaString());
				});
				Console.WriteLine("> Busy");
				await tr.Snapshot.GetRange(busyLocation.Keys.ToRange()).ForEachAsync((kvp) =>
				{
					Console.WriteLine("- Busy." + busyLocation.Keys.Unpack(kvp.Key) + " = " + kvp.Value.ToAsciiOrHexaString());
				});
				Console.WriteLine("> Unassigned");
				await tr.Snapshot.GetRange(unassignedLocation.Keys.ToRange()).ForEachAsync((kvp) =>
				{
					Console.WriteLine("- Unassigned." + unassignedLocation.Keys.Unpack(kvp.Key) + " = " + kvp.Value.ToAsciiOrHexaString());
				});
				Console.WriteLine("> Tasks");
				await tr.Snapshot.GetRange(tasksLocation.Keys.ToRange()).ForEachAsync((kvp) =>
				{
					Console.WriteLine("- Tasks." + tasksLocation.Keys.Unpack(kvp.Key) + " = " + kvp.Value.ToAsciiOrHexaString());
				});
				Console.WriteLine("<");
			}
		}

		#region IAsyncTest...

		public string Name { get { return "MessageQueueTest"; } }

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			await Init(db, ct);
			log.WriteLine("Message queue test initialized");

			switch(this.Role)
			{
				case AgentRole.Worker:
				{
					log.WriteLine("Running as Consumer '" + this.Id + "'");
					await RunConsumer(db, ct);
					break;
				}
				case AgentRole.Producer:
				{
					log.WriteLine("Running as Producer '" + this.Id + "'");
					await RunProducer(db, ct);
					break;
				}
				case AgentRole.Clear:
				{
					log.WriteLine("Running as Clear '" + this.Id + "'");
					await RunClear(db, ct);
					break;
				}
				case AgentRole.Status:
				{
					log.WriteLine("Running as Observer '" + this.Id + "'");
					await RunStatus(db, ct);
					break;
				}
			}
		}

		#endregion

	}

}
