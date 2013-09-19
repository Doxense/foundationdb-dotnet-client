using FoundationDB.Client;
using FoundationDB.Samples.Tutorials;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Samples
{

	public interface IAsyncTest
	{
		string Name { get; }
		Task Run(FdbDatabase db, CancellationToken ct);
	}

	public static class TestRunner
	{
		public static void RunAsyncTest(IAsyncTest test, FdbDatabase db)
		{
			Console.WriteLine("Starting " + test.Name + " ...");

			var cts = new CancellationTokenSource();
			try
			{
				var t = test.Run(db, cts.Token);

				t.GetAwaiter().GetResult();
				Console.WriteLine("Completed " + test.Name + ".");
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.ToString());
			}
			finally
			{
				cts.Dispose();
			}
		}

		public static async Task<TimeSpan> RunConcurrentWorkersAsync(int workers, 
			Func<int, CancellationToken, Task> handler, 
			CancellationToken ct)
		{
			var signal = new TaskCompletionSource<object>();
			var tasks = Enumerable.Range(0, workers).Select(async (i) =>
			{
				await signal.Task;
				ct.ThrowIfCancellationRequested();
				await handler(i, ct);
			}).ToArray();

			var sw = Stopwatch.StartNew();
			ThreadPool.QueueUserWorkItem((_) => { signal.SetResult(null); });
			await Task.WhenAll(tasks);
			sw.Stop();

			return sw.Elapsed;
		}

	}

	class Program
	{
		private static FdbDatabase Db;

		static void Main(string[] args)
		{
			bool stop = false;

			// Initialize FDB

			Fdb.Start();

			Db = Fdb.OpenAsync().Result;

			Console.WriteLine("Using API v" + Fdb.GetMaxApiVersion());
			Console.WriteLine("FoundationDB Samples menu:");
			Console.WriteLine("Press '1' for ClassSchedudling, '2' for TBD, or 'q' to exit");

			Console.WriteLine("Ready...");

			while(!stop)
			{
				Console.Write("> ");
				string s = Console.ReadLine();

				switch(s.Trim().ToLowerInvariant())
				{
					case "":
					{
						continue;
					}
					case "1":
					{ // Class Scheduling

						TestRunner.RunAsyncTest(new ClassScheduling(), Db);
						break;
					}

					case "q":
					case "x":
					{
						stop = true;
						break;
					}

					default:
					{
						Console.WriteLine("Unknown command");
						break;
					}
				}
			}

			Fdb.Stop();

			Console.WriteLine("Bye");

		}
	}
}
