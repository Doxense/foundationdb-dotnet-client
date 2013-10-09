#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client.Filters.Logging.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;
	using System;
	using System.Linq;
	using System.Threading.Tasks;

	[TestFixture]
	public class LoggingFilterFacts
	{

		[Test]
		public async Task Test_Can_Log_A_Transaction()
		{
			//await Task.Delay(10).ConfigureAwait(false);
			const int N = 10;

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = await db.CreateOrOpenDirectoryAsync(new[] { "Logging" });
				await db.ClearRangeAsync(location);

				await db.WriteAsync((tr) =>
				{
					var rnd = new Random();
					tr.Set(location.Pack("One"), Slice.FromString("111111"));
					tr.Set(location.Pack("Two"), Slice.FromString("222222"));
					for (int j = 0; j < 4; j++)
					{
						for (int i = 0; i < 100; i++)
						{
							tr.Set(location.Pack("Range", j, rnd.Next(1000)), Slice.Empty);
						}
					}
					for (int j = 0; j < N; j++)
					{
						tr.Set(location.Pack("X", j), Slice.FromInt32(j));
						tr.Set(location.Pack("Y", j), Slice.FromInt32(j));
					}
				});

				bool first = true;

				Action<FdbLoggedTransaction> logHandler = (tr) =>
				{
					if (first)
					{
						Console.WriteLine(tr.Log.GetCommandsReport());
						first = false;
					}

					Console.WriteLine(tr.Log.GetTimingsReport());
				};

				// create a logged version of the database
				var logged = new FdbLoggedDatabase(db, false, false, logHandler);

				for (int k = 0; k < 10; k++)
				{
					Console.WriteLine("==== " + k + " ==== ");
					Console.WriteLine();

					await logged.ReadWriteAsync(async (tr) =>
					{
						Assert.That(tr, Is.InstanceOf<FdbLoggedTransaction>());

						tr.Set(location.Pack("Write"), Slice.FromString("abcdef"));
						tr.Clear(location.Pack("Clear", "0"));
						tr.ClearRange(location.Pack("Clear", "A"), location.Pack("Clear", "Z"));

						await tr.GetAsync(location.Pack("One"));
						await tr.GetAsync(location.Pack("NotFound"));

						await tr.GetRangeAsync(FdbKeySelector.LastLessOrEqual(location.Pack("A")), FdbKeySelector.FirstGreaterThan(location.Pack("Z")));

						await Task.WhenAll(
							tr.GetRange(FdbKeyRange.StartsWith(location.Pack("Range", 0))).ToListAsync(),
							tr.GetRange(location.Pack("Range", 1, 0), location.Pack("Range", 1, 200)).ToListAsync(),
							tr.GetRange(location.Pack("Range", 2, 400), location.Pack("Range", 2, 600)).ToListAsync(),
							tr.GetRange(location.Pack("Range", 3, 800), location.Pack("Range", 3, 1000)).ToListAsync()
						);

						await tr.GetAsync(location.Pack("Two"));

						await tr.GetValuesAsync(Enumerable.Range(0, N).Select(x => location.Pack("X", x)));

						for (int i = 0; i < N; i++)
						{
							await tr.GetAsync(location.Pack("X", i));
						}

						await Task.WhenAll(Enumerable.Range(0, N/2).Select(x => tr.GetAsync(location.Pack("Y", x))));
						await Task.WhenAll(Enumerable.Range(N/2, N/2).Select(x => tr.GetAsync(location.Pack("Y", x))));
					});
				}
			}
		}

	}

}
