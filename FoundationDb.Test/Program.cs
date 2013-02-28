using System;
using System.Data.FoundationDb.Client;
using System.Threading.Tasks;

namespace FoundationDb.Test
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
				ExecuteAsync(async () =>
				{
					FdbCore.NativeLibPath = @"C:\Program Files\foundationdb\bin";

					Console.WriteLine(FdbCore.GetMaxApiVersion());

					try
					{
						FdbCore.Start();
						Console.WriteLine("Done!");

						using (var cluster = await FdbCore.ConnectAsync(null))
						{

							using (var db = await cluster.OpenDatabaseAsync("DB"))
							{
								Console.WriteLine("Connected to db '{0}'", db.Name);

								using (var trans = db.BeginTransaction())
								{
									Console.WriteLine("Got the transaction!");

									//trans.Set("Hello", "World");
									//var data = new byte[512];
									//new Random(1234).NextBytes(data);
									//trans.Set("TopSecret", data);

									Console.WriteLine("Commiting...");
									await trans.CommitAsync();
									Console.WriteLine("Committed!");
								}
							}

						}

					}
					finally
					{
						Console.WriteLine("### DONE ###");
						FdbCore.Stop();
					}
				});
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Oops! something went wrong:");
				Console.Error.WriteLine(e.ToString());
			}
			Console.WriteLine("[PRESS A KEY TO EXIT]");
			Console.ReadKey();

		}
	}
}
