#region BSD License
/* Copyright (c) 2013-2023 Doxense SAS
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

namespace FoundationDB.Filters.Logging.Tests
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class LoggingFilterFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Log_A_Transaction()
		{
			const int N = 10;

			using (var db = await OpenTestPartitionAsync())
			{
				// get a tuple view of the directory
				var location = db.Root["Logging"];
				await CleanLocation(db, location);

				// note: ensure that all methods are JITed
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);

					await tr.GetReadVersionAsync();
					tr.Set(subspace.Encode("Warmup", 0), Slice.FromInt32(1));
					tr.Clear(subspace.Encode("Warmup", 1));
					await tr.GetAsync(subspace.Encode("Warmup", 2));
					await tr.GetRange(KeyRange.StartsWith(subspace.Encode("Warmup", 3))).ToListAsync();
					tr.ClearRange(subspace.Encode("Warmup", 4), subspace.Encode("Warmup", 5));
				}, this.Cancellation);

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);

					var rnd = new Random();
					tr.Set(subspace.Encode("One"), Value("111111"));
					tr.Set(subspace.Encode("Two"), Value("222222"));
					for (int j = 0; j < 4; j++)
					{
						for (int i = 0; i < 100; i++)
						{
							tr.Set(subspace.Encode("Range", j, rnd.Next(1000)), Slice.Empty);
						}
					}
					for (int j = 0; j < N; j++)
					{
						tr.Set(subspace.Encode("X", j), Slice.FromInt32(j));
						tr.Set(subspace.Encode("Y", j), Slice.FromInt32(j));
						tr.Set(subspace.Encode("Z", j), Slice.FromInt32(j));
						tr.Set(subspace.Encode("W", j), Slice.FromInt32(j));
					}
				}, this.Cancellation);

				bool first = true;
				Action<FdbTransactionLog> logHandler = (log) =>
				{
					if (first)
					{
						Log(log.GetCommandsReport());
						first = false;
					}

					Log(log.GetTimingsReport(true));
				};

				// create a logged version of the database
				db.SetDefaultLogHandler(logHandler);

				for (int k = 0; k < N; k++)
				{
					Log("==== " + k + " ==== ");
					Log();

					await db.WriteAsync(async (tr) =>
					{
						Assert.That(tr.Log, Is.Not.Null);
						Assert.That(tr.IsLogged(), Is.True);

						var subspace = await location.Resolve(tr);

						//tr.SetOption(FdbTransactionOption.CausalReadRisky);

						long ver = await tr.GetReadVersionAsync().ConfigureAwait(false);

						await tr.GetAsync(subspace.Encode("One")).ConfigureAwait(false);
						await tr.GetAsync(subspace.Encode("NotFound")).ConfigureAwait(false);

						tr.Set(subspace.Encode("Write"), Value("abcdef" + k.ToString()));

						//tr.Annotate("BEFORE");
						//await Task.Delay(TimeSpan.FromMilliseconds(10));
						//tr.Annotate("AFTER");

						//await tr.Snapshot.GetAsync(folder.Pack("Snap")).ConfigureAwait(false);

						tr.Annotate("This is a comment");

						//await tr.GetRangeAsync(KeySelector.LastLessOrEqual(folder.Pack("A")), KeySelector.FirstGreaterThan(folder.Pack("Z"))).ConfigureAwait(false);

						await Task.WhenAll(
							tr.GetRange(KeyRange.StartsWith(subspace.Encode("Range", 0))).ToListAsync(),
							tr.GetRange(subspace.Encode("Range", 1, 0), subspace.Encode("Range", 1, 200)).ToListAsync(),
							tr.GetRange(subspace.Encode("Range", 2, 400), subspace.Encode("Range", 2, 600)).ToListAsync(),
							tr.GetRange(subspace.Encode("Range", 3, 800), subspace.Encode("Range", 3, 1000)).ToListAsync()
						).ConfigureAwait(false);

						await tr.GetAsync(subspace.Encode("Two")).ConfigureAwait(false);

						await tr.GetValuesAsync(Enumerable.Range(0, N).Select(x => subspace.Encode("X", x))).ConfigureAwait(false);

						for (int i = 0; i < N; i++)
						{
							await tr.GetAsync(subspace.Encode("Z", i)).ConfigureAwait(false);
						}

						await Task.WhenAll(Enumerable.Range(0, N / 2).Select(x => tr.GetAsync(subspace.Encode("Y", x)))).ConfigureAwait(false);
						await Task.WhenAll(Enumerable.Range(N / 2, N / 2).Select(x => tr.GetAsync(subspace.Encode("Y", x)))).ConfigureAwait(false);

						await Task.WhenAll(
							tr.GetAsync(subspace.Encode("W", 1)),
							tr.GetAsync(subspace.Encode("W", 2)),
							tr.GetAsync(subspace.Encode("W", 3))
						).ConfigureAwait(false);

						tr.Set(subspace.Encode("Write2"), Value("ghijkl" + k.ToString()));
						tr.Clear(subspace.Encode("Clear", "0"));
						tr.ClearRange(subspace.Encode("Clear", "A"), subspace.Encode("Clear", "Z"));

						if (tr.Context.Retries == 0)
						{
							// make it fail
							//throw new FdbException(FdbError.TransactionTooOld, "fake timeout");
						}

					}, this.Cancellation);
				}
			}
		}

	}

}
