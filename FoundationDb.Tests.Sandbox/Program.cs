using System;
using FoundationDb.Client;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

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
				var readVersion = await trans.GetReadVersion();
				Console.WriteLine("> Read Version = " + readVersion);

				Console.WriteLine("Getting 'hello'...");
				var result = await trans.GetAsync("hello");
				//var result = trans.Get("hello");
				if (result == null)
					Console.WriteLine("> hello NOT FOUND");
				else
					Console.WriteLine("> hello = " + Encoding.UTF8.GetString(result));

				Console.WriteLine("Setting 'Foo' = 'Bar'");
				trans.Set("Foo", "Bar");

				Console.WriteLine("Setting 'TopSecret' = rnd(512)");
				var data = new byte[512];
				new Random(1234).NextBytes(data);
				trans.Set("TopSecret", data);

				Console.WriteLine("Committing transaction...");
				await trans.CommitAsync();
				//trans.Commit();
				Console.WriteLine("> Committed!");
			}
		}

		static async Task BenchInsertSmallKeysAsync(FdbDatabase db, int N)
		{
			// insert a lot of small keys, in a single transaction

			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				for (int i = 0; i < N; i++)
				{
					trans.Set("hello" + i, "world" + i);
				}
				await trans.CommitAsync();
			}
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed + " to insert " + N + " items (" + (sw.Elapsed.TotalMilliseconds / N) + "/write)");
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
			Console.WriteLine("Took " + sw.Elapsed + " to read " + N + " items (" + (sw.Elapsed.TotalMilliseconds / N) + "/read)");
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
			Console.WriteLine("Took " + sw.Elapsed + " to read " + N + " items (" + (sw.Elapsed.TotalMilliseconds / N) + "/read)");

			var keys = Enumerable.Range(0, N).Select(i => "hello" + i).ToArray();

			sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction())
			{
				var results = await trans.GetBatchAsync(keys);
			}
			sw.Stop();
			Console.WriteLine("Took " + sw.Elapsed + " to read " + keys.Length + " items (" + (sw.Elapsed.TotalMilliseconds / keys.Length) + "/read)");
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
			Console.WriteLine("Took " + sw.Elapsed + " to read " + N + " items (" + (sw.Elapsed.TotalMilliseconds / N) + "/read)");
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
			Console.WriteLine("Took " + sw.Elapsed + " to clear " + N + " items (" + (sw.Elapsed.TotalMilliseconds / N) + "/write)");
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
					trans.Set("list", list);
					await trans.CommitAsync();
				}
			}
			update.Stop();

			Console.WriteLine("Took " + update.Elapsed + " to fill a byte[" + N + "] one by one (" + (update.Elapsed.TotalMilliseconds / N) + "/write)");
		}

		static async Task BenchUpdateLotsOfKeysAsync(FdbDatabase db, int N)
		{
			// continuously update same key by adding a little bit more

			var keys = Enumerable.Range(0, N).Select(x => "list" + x.ToString()).ToArray();

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

			Console.WriteLine("Took " + sw.Elapsed + " to change a byte in " + N + " lists (" + (sw.Elapsed.TotalMilliseconds / N) + "/write)");

		}

		static async Task MainAsync(string[] args)
		{
			const int N = 1000;
			const string NATIVE_PATH = @"C:\Program Files\foundationdb\bin";

			FdbCore.NativeLibPath = NATIVE_PATH;

			// uncomment this to enable network thread tracing
			//FdbCore.TracePath = Path.Combine(Path.GetTempPath(), "fdb");

			int apiVersion = FdbCore.GetMaxApiVersion();
			Console.WriteLine("Max API Version: " + apiVersion);

			try
			{
				Console.WriteLine("Starting network thread...");
				FdbCore.Start(); // this will select API version 21			
				Console.WriteLine("> Up and running");

				Console.WriteLine("Connecting to local cluster...");
				using (var cluster = await FdbCore.OpenLocalClusterAsync())
				{
					Console.WriteLine("> Connected!");

					Console.WriteLine("Opening database 'DB'...");
					using (var db = await cluster.OpenDatabaseAsync("DB"))
					{
						Console.WriteLine("> Connected to db '{0}'", db.Name);

						//await TestSimpleTransactionAsync(db);

						//await BenchInsertSmallKeysAsync(db, N);

						//await BenchSerialReadAsync(db, N);

						//await BenchConcurrentReadAsync(db, N);

						//BenchSerialReadBlocking(db, N);

						//await BenchClearAsync(db, N);

						//await BenchUpdateSameKeyLotsOfTimesAsync(db, N);

						await BenchUpdateLotsOfKeysAsync(db, N);

						Console.WriteLine("time to say goodbye...");
					}
				}
			}
			finally
			{
				Console.WriteLine("### DONE ###");
				FdbCore.Stop();
			}
		}
	}
}
