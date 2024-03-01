#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client.Testing
{
	using System;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;

	public static class Program
	{

		public static async Task Main(string[] args)
		{

			var a = AppContext.BaseDirectory;
			var b = AppContext.TargetFrameworkName;
			AppContext.GetData("hello");

			using var cts = new CancellationTokenSource();
			var ct = cts.Token;

			var builder = Host.CreateApplicationBuilder(args);
			builder.Services.AddFoundationDb(710);
			var host = builder.Build();

			var dbProvider = host.Services.GetRequiredService<IFdbDatabaseProvider>();

			var db = await dbProvider.GetDatabase(ct);

			{
				var test = TestSuiteBuilder
					.ParseTestDump(MapPathRelativeToProject("./Dumps/test_api.txt"))
					.Build();

				using (var tester = new StackMachine(db, test.Prefix, host.Services.GetRequiredService<ILogger<StackMachine>>()))
				{
					await tester.Run(test, ct);
				}
			}

			Console.WriteLine("Done!");
		}

		public static string MapPathRelativeToProject(string path, [CallerFilePath] string callerPath = "")
		{
			string folder = Path.GetDirectoryName(callerPath)!;
			return Path.GetFullPath(Path.Combine(folder, path));
		}

	}

}
