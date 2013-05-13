using System;
using FoundationDb.Client;
using FoundationDb.Client.Tuples;
using FoundationDb.Client.Tables;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace FoundationDb.Tests.Sandbox
{
	class Program
	{

		static void ExecuteAsync(Func<Task> code)
		{
			// poor man's async main loop
			Task.Run(code).GetAwaiter().GetResult();
		}

		static void Main(string[] args)
		{
			try
			{
				ExecuteAsync(() => MainAsync(args));
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Oops! something went wrong:");
				Console.Error.WriteLine(e.ToString());
			}
			Console.WriteLine("[PRESS A KEY TO EXIT]");
			Console.ReadKey();
		}

		static async Task TestSimpleTransactionAsync(FdbDatabase db)
		{
			Console.WriteLine("Starting new transaction...");
			using (var trans = db.BeginTransaction())
			{
				Console.WriteLine("> Transaction ready");

				Console.WriteLine("Getting read version...");
				var readVersion = await trans.GetReadVersionAsync();
				Console.WriteLine("> Read Version = " + readVersion);

				Console.WriteLine("Getting 'hello'...");
				var result = await trans.GetAsync("hello");
				//var result = trans.Get("hello");
				if (result == null)
					Console.WriteLine("> hello NOT FOUND");
				else
					Console.WriteLine("> hello = " + Encoding.UTF8.GetString(result));

				Console.WriteLine("Setting 'Foo' = 'Bar'");
				trans.Set(FdbKey.Ascii("Foo"), FdbValue.Encode("Bar"));

				Console.WriteLine("Setting 'TopSecret' = rnd(512)");
				var data = new byte[512];
				new Random(1234).NextBytes(data);
				trans.Set(FdbKey.Ascii("TopSecret"), data);

				Console.WriteLine("Committing transaction...");
				await trans.CommitAsync();
				//trans.Commit();
				Console.WriteLine("> Committed!");

				Console.WriteLine("Getting comitted version...");
				var writeVersion = trans.GetCommittedVersion();
				Console.WriteLine("> Commited Version = " + writeVersion);
			}
		}

		static async Task BenchInsertSmallKeysAsync(FdbDatabase db, int N, int size)
		{
			// insert a lot of small key size, in a single transaction
			var rnd = new Random();
			var tmp = new byte[size];

			var table = FdbTuple.Create("Batch");

			var times = new List<TimeSpan>();
			for (int k = 0; k <= 4; k++)
			{
				var sw = Stopwatch.StartNew();
				using (var trans = db.BeginTransaction())
				{
					rnd.NextBytes(tmp);
					for (int i = 0; i < N; i++)
					{
						tmp[0] = (byte)i;
						tmp[1] = (byte)(i >> 8);
						trans.Set(table.Append(k * N + i).ToArraySegment(), new ArraySegment<byte>(tmp));
					}
					await trans.CommitAsync();
				}
				sw.Stop();
				times.Add(sw.Elapsed);
			}
			var min = times.Min();
			Console.WriteLine("Took " + min.TotalSeconds.ToString("N3") + " to insert " + N + " " + size + "-bytes items (" + FormatTimeMicro(min.TotalMilliseconds / N) + "/write)");
		}

		static async Task BenchConcurrentInsert(FdbDatabase db, int k, int N, int size)
		{
			// insert a lot of small key size, in multiple batch running in //

			int n = N / k;
			N = n * k;

			var table = FdbTuple.Create("Batch");

			var tasks = new Task[k];
			var sem = new ManualResetEventSlim();
			for (int j = 0; j < k; j++)
			{
				int offset = j * n;
				tasks[j] = Task.Factory.StartNew(async () =>
				{
					var rnd = new Random();
					var tmp = new byte[size];
					rnd.NextBytes(tmp);

					sem.Wait();
					using (var trans = db.BeginTransaction())
					{
						for (int i = 0; i < n; i++)
						{
							tmp[0] = (byte)i;
							tmp[1] = (byte)(i >> 8);
							trans.Set(table.Append(offset + i).ToArraySegment(), new ArraySegment<byte>(tmp));
						}
						await trans.CommitAsync();
					}
				}, TaskCreationOptions.LongRunning).Unwrap();
			}
			await Task.Delay(100);
			var sw = Stopwatch.StartNew();
			sem.Set();
			await Task.WhenAll(tasks);
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed.TotalSeconds.ToString("N3") + " to insert " + N + " " + size + "-bytes items on " + k + " threads (" + FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N) + "/write)");
		}

		static async Task BenchSerialReadAsync(FdbDatabase db, int N)
		{
			// read a lot of small keys, one by one

			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				for (int i = 0; i < N; i++)
				{
					var result = await trans.GetAsync("hello" + i);
				}
			}
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed + " to read " + N + " items (" + FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N) + "/read)");
		}

		static async Task BenchConcurrentReadAsync(FdbDatabase db, int N)
		{
			// read a lot of small keys, concurrently

			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				var results = await Task.WhenAll(Enumerable
					.Range(0, N)
					.Select((i) => trans.GetAsync("hello" + i))
				);
			}
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed + " to read " + N + " items (" + FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N) + "/read)");

			var keys = Enumerable.Range(0, N).Select(i => FdbKey.Ascii("hello" + i)).ToArray();

			sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				var results = await trans.GetBatchAsync(keys);
			}
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed + " to read " + keys.Length + " items (" + FormatTimeMicro(sw.Elapsed.TotalMilliseconds / keys.Length) + "/read)");
		}

		static void BenchSerialReadBlocking(FdbDatabase db, int N)
		{
			// read a lot of small keys, by blocking (no async)

			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				for (int i = 0; i < N; i++)
				{
					var result = trans.Get("hello" + i);
				}
			}
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed + " to read " + N + " items (" + FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N) + "/read)");
		}

		static async Task BenchClearAsync(FdbDatabase db, int N)
		{
			// clear a lot of small keys, in a single transaction

			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				for (int i = 0; i < N; i++)
				{
					trans.Clear("hello" + i);
				}

				await trans.CommitAsync();
			}
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed + " to clear " + N + " items (" + FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N) + "/write)");
		}

		static async Task BenchUpdateSameKeyLotsOfTimesAsync(FdbDatabase db, int N)
		{
			// continuously update same key by adding a little bit more

			var list = new byte[N];
			var update = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				list[i] = (byte)i;
				using (var trans = db.BeginTransaction())
				{
					trans.Set(FdbKey.Ascii("list"), list);
					await trans.CommitAsync();
				}
			}
			update.Stop();

			Console.WriteLine("Took " + update.Elapsed + " to fill a byte[" + N + "] one by one (" + FormatTimeMicro(update.Elapsed.TotalMilliseconds / N) + "/write)");
		}

		static async Task BenchUpdateLotsOfKeysAsync(FdbDatabase db, int N)
		{
			// continuously update same key by adding a little bit more

			var keys = Enumerable.Range(0, N).Select(x => FdbKey.Ascii("list" + x.ToString())).ToArray();

			Console.WriteLine("> creating " + N + " half filled keys");
			var segment = new byte[60];

			for (int i = 0; i < (segment.Length >> 1); i++) segment[i] = (byte)(i >> 2);
			using (var trans = db.BeginTransaction())
			{
				for (int i = 0; i < N; i++)
				{
					trans.Set(keys[i], segment);
				}
				await trans.CommitAsync();
			}

			Console.WriteLine("Changing one byte in each of the " + N + " keys...");
			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				Console.WriteLine("READ");
				// get all the lists
				var data = await trans.GetBatchAsync(keys);

				// change them
				Console.WriteLine("CHANGE");
				for (int i = 0; i < data.Count; i++)
				{
					var list = data[i].Value;
					list[(list.Length >> 1) + 1] = (byte)i;
					trans.Set(data[i].Key, list);
				}

				Console.WriteLine("COMMIT");
				await trans.CommitAsync();
			}
			sw.Stop();

			Console.WriteLine("Took " + sw.Elapsed + " to change a byte in " + N + " lists (" + FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N) + " /write)");

		}

		private static string FormatTimeMicro(double ms)
		{
			return (1000 * ms).ToString("N1") + "µs";
		}

		static async Task MainAsync(string[] args)
		{
			const int N = 16384;
			const string NATIVE_PATH = @"C:\Program Files\foundationdb\bin";

			Fdb.NativeLibPath = NATIVE_PATH;

			// uncomment this to enable network thread tracing
			//FdbCore.TracePath = Path.Combine(Path.GetTempPath(), "fdb");

			int apiVersion = Fdb.GetMaxApiVersion();
			Console.WriteLine("Max API Version: " + apiVersion);

			try
			{
				Console.WriteLine("Starting network thread...");
				Fdb.Start(); // this will select API version 21			
				Console.WriteLine("> Up and running");

				Console.WriteLine("Connecting to local cluster...");
				using (var cluster = await Fdb.OpenLocalClusterAsync())
				{
					Console.WriteLine("> Connected!");

					Console.WriteLine("Opening database 'DB'...");
					using (var db = await cluster.OpenDatabaseAsync("DB"))
					{
						Console.WriteLine("> Connected to db '{0}'", db.Name);

						// clear everything
						using (var tr = db.BeginTransaction())
						{
							Console.WriteLine("Clearing database...");
							tr.ClearRange(FdbKey.MinValue, FdbKey.MaxValue);
							await tr.CommitAsync();
							Console.WriteLine("> Database cleared");
						}

						await TestSimpleTransactionAsync(db);

						//await BenchInsertSmallKeysAsync(db, N, 16); // some guid
						//await BenchInsertSmallKeysAsync(db, N, 60 * 4); // one Int32 per minutes, over an hour
						await BenchInsertSmallKeysAsync(db, N, 512); // small JSON payload
						//await BenchInsertSmallKeysAsync(db, N, 4096); // typical small cunk size
						//await BenchInsertSmallKeysAsync(db, N / 10, 65536); // typical medium chunk size
						//await BenchInsertSmallKeysAsync(db, 1, 100000); // Maximum value size (as of beta 1)

						// insert keys in parrallel
						await BenchConcurrentInsert(db, 2, N, 512);
						await BenchConcurrentInsert(db, 4, N, 512);
						await BenchConcurrentInsert(db, 8, N, 512);
						await BenchConcurrentInsert(db, 16, N, 512);

						//await BenchSerialReadAsync(db, N);

						//await BenchConcurrentReadAsync(db, N);

						//BenchSerialReadBlocking(db, N);

						//await BenchClearAsync(db, N);

						//await BenchUpdateSameKeyLotsOfTimesAsync(db, N);

						//await BenchUpdateLotsOfKeysAsync(db, N);

						//var k1 = FdbKey.Ascii("hello world");
						//Console.WriteLine(k1.ToString());
						//Console.WriteLine(ToHexArray(k1.ToBytes()));

						//var k2 = FdbKey.Pack("hello world", 123);
						//Console.WriteLine(k2.ToString());
						//Console.WriteLine(ToHexArray(k2.ToBytes()));

						//var k2b = FdbTuple.Create("hello world", 123);
						//Console.WriteLine(k2b.ToString());

						//var k3 = FdbKey.Pack(k2b, "yolo");
						//Console.WriteLine(k3.ToString());
						//Console.WriteLine(ToHexArray(k3.ToBytes()));

						//var foos = db.Table("foos");
						//Console.WriteLine(ToHexArray(foos.GetKeyBytes("hello")));
						//Console.WriteLine(ToHexArray(foos.GetKeyBytes(new byte[] { 65, 66, 67 })));
						//Console.WriteLine(ToHexArray(foos.GetKeyBytes(FdbTuple.Create("hello", 123))));

						//string rndid = Guid.NewGuid().ToString();
						//Console.WriteLine(ToHexArray(foos.GetKeyBytes(rndid)));

						//var key = foos.Key(123);
						//Console.WriteLine(key.Count);
						//Console.WriteLine(String.Join(", ", key.ToArray()));

						//using (var trans = db.BeginTransaction())
						//{
						//	foos.Set(trans, rndid, Encoding.UTF8.GetBytes("This is the value of " + rndid));
						//	await trans.CommitAsync();
						//}

						//using (var trans = db.BeginTransaction())
						//{
						//	byte[] value = await foos.GetAsync(trans, rndid);
						//	Console.WriteLine(ToHexArray(value));
						//	value = await trans.GetAsync(FdbTuple.Create("foos", rndid));
						//	Console.WriteLine(ToHexArray(value));
						//}


						//// test range
						//using (var trans = db.BeginTransaction())
						//{
						//	var prefix = FdbTuple.Create("range");
						//	for (int i = 0; i < 100; i++)
						//	{
						//		var k = prefix.Append(i);
						//		Console.WriteLine("Insert: " + ToHexString(k.ToArraySegment()));
						//		trans.Set(k, "value" + i.ToString());
						//	}
						//	await trans.CommitAsync();
						//}

						//using (var trans = db.BeginTransaction())
						//{
						//	Console.WriteLine("Begin: " + ToHexString(FdbKey.Pack("range", 1).ToArraySegment()));
						//	Console.WriteLine("End: " + ToHexString(FdbKey.Pack("range", 7).ToArraySegment()));

						//	var res = await trans.GetRangeAsync(
						//		FdbKeySelector.FirstGreaterOrEqual(FdbKey.Pack("range", 1).ToArraySegment()),
						//		FdbKeySelector.LastLessOrEqual(FdbKey.Pack("range", 7).ToArraySegment()) + 1,
						//		0, 
						//		0,
						//		FDBStreamingMode.WantAll,
						//		0,
						//		false,
						//		false
						//	);

						//	Console.WriteLine("Found " + res.Page.Length + " results");
						//	foreach (var x in res.Page)
						//	{
						//		Console.WriteLine(ToHexString(x.Key) + " : " + Encoding.UTF8.GetString(x.Value.Array, x.Value.Offset, x.Value.Count));
						//	}
						//}

						Console.WriteLine("time to say goodbye...");
					}
				}
			}
			finally
			{
				Console.WriteLine("### DONE ###");
				Fdb.Stop();
			}
		}

		private static string ToHexString(ArraySegment<byte> segment)
		{
			if (segment.Array == null) return "<null>";
			if (segment.Count == 0) return "<empty>";
			// close you eyes...
			return String.Join(" ", segment.Array.Skip(segment.Offset).Take(segment.Count).Select(b => b.ToString("X2")));
		}

		private static string ToHexArray(byte[] buffer)
		{
			var sb = new StringBuilder();
			sb.Append("[" + buffer.Length + "]{");
			for (int i = 0; i < buffer.Length; i++)
			{
				if (i == 0) sb.Append(' '); else sb.Append(", ");
				sb.Append(buffer[i]);
			}
			sb.AppendLine("}");
			sb.Append("> \"" + Encoding.UTF8.GetString(buffer) + "\"");
			return sb.ToString();
		}

		private static string ToHexArray(ArraySegment<byte> buffer)
		{
			var sb = new StringBuilder();
			sb.Append("[" + buffer.Count + "]{");
			for(int i=0;i<buffer.Count;i++)
			{
				if (i == 0) sb.Append(' '); else sb.Append(", ");
				sb.Append(buffer.Array[buffer.Offset + i]);
			}
			sb.AppendLine("}");
			sb.Append(System.Web.HttpUtility.JavaScriptStringEncode(Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count)));
			return sb.ToString();
		}


	}
}
