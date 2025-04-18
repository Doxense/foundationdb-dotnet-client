#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

// ReSharper disable ConvertToUsingDeclaration
// ReSharper disable StringLiteralTypo

namespace FoundationDB.Client.Tests
{

	[TestFixture][Ignore("These tests are not meant to be run as part of a CI build")]
	public class ExoticTestCases : FdbTest
	{
		// This is a collection of specific test cases, used to trigger specific behaviors from the client
		// => THEY ARE NOT TESTING THE DATABASE ITSELF, ONLY USED AS TOOLS TO OBSERVE THE CHANGES TO THE DATABASE!

		[Test]
		public async Task Test_Case_1()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Encode("AAA"), Text("111"));
					tr.Set(subspace.Encode("BBB"), Text("222"));
					tr.Set(subspace.Encode("CCC"), Text("333"));
					tr.Set(subspace.Encode("DDD"), Text("444"));
					tr.Set(subspace.Encode("EEE"), Text("555"));
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_2()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace.Encode("AAA"), Text("ZZZ"));
					tr.Set(subspace.Encode("AAA"), Text("111"));
					tr.Set(subspace.Encode("BBB"), Text("222"));
					tr.Set(subspace.Encode("CCC"), Text("333"));
					tr.Set(subspace.Encode("DDD"), Text("444"));
					tr.Set(subspace.Encode("EEE"), Text("555"));
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_3()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace.Encode("AAA"), Text("BBB"));
					tr.ClearRange(subspace.Encode("BBB"), Text("CCC"));
					tr.ClearRange(subspace.Encode("CCC"), Text("DDD"));
					// should be merged into a single AAA...DDD
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_4()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);

					//initial setup:
					// A: none
					// B: 0
					// C: 255
					// D: none
					// E: none
					tr.Set(subspace.Encode("BBB"), Slice.FromFixed32(0));
					tr.Set(subspace.Encode("CCC"), Slice.FromFixed32(255));

					// add 1 to everybody
					tr.AtomicAdd32(subspace.Encode("AAA"), 1);
					tr.AtomicAdd32(subspace.Encode("BBB"), -1);
					tr.AtomicAdd32(subspace.Encode("CCC"), 1U);
					tr.AtomicAdd64(subspace.Encode("DDD"), 1L);
					tr.AtomicAdd64(subspace.Encode("EEE"), 1UL);

					// overwrite DDD with a fixed value
					tr.Set(subspace.Encode("DDD"), Slice.FromFixed32(5));
					// double add on EEE
					tr.AtomicAdd32(subspace.Encode("EEE"), 1);

					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_5()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Encode("AAA"), Text("111"));
					tr.AtomicAdd(subspace.Encode("BBB"), Text("222"));
					tr.AtomicAnd(subspace.Encode("CCC"), Text("333"));
					tr.AtomicOr(subspace.Encode("DDD"), Text("444"));
					tr.AtomicXor(subspace.Encode("EEE"), Text("555"));
					tr.AtomicMax(subspace.Encode("FFF"), Text("666"));
					tr.AtomicMin(subspace.Encode("GGG"), Text("777"));
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_6()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);

					tr.AtomicMax(subspace.Encode("MAXMAX1"), Text("EEE"));
					tr.AtomicMax(subspace.Encode("MAXMAX1"), Text("FFF"));

					tr.AtomicMax(subspace.Encode("MAXMAX2"), Text("FFF"));
					tr.AtomicMax(subspace.Encode("MAXMAX2"), Text("EEE"));

					tr.AtomicMin(subspace.Encode("MINMIN1"), Text("111"));
					tr.AtomicMin(subspace.Encode("MINMIN1"), Text("222"));

					tr.AtomicMin(subspace.Encode("MINMIN2"), Text("222"));
					tr.AtomicMin(subspace.Encode("MINMIN2"), Text("111"));

					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_6b()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				var init = Slice.Repeat(0xCC, 9);
				var mask = Slice.Repeat(0xAA, 9);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);

					tr.Set(subspace.Encode("AAA"), init);
					tr.Set(subspace.Encode("BBB"), init);
					tr.Set(subspace.Encode("CCC"), init);
					tr.Set(subspace.Encode("DDD"), init);
					tr.Set(subspace.Encode("EEE"), init);
					tr.Set(subspace.Encode("FFF"), init);
					tr.Set(subspace.Encode("GGG"), init);

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);

					tr.Set(subspace.Encode("AAA"), mask);
					tr.AtomicAdd(subspace.Encode("BBB"), mask);
					tr.AtomicAnd(subspace.Encode("CCC"), mask);
					tr.AtomicOr(subspace.Encode("DDD"), mask);
					tr.AtomicXor(subspace.Encode("EEE"), mask);
					tr.AtomicMin(subspace.Encode("FFF"), mask);
					tr.AtomicMax(subspace.Encode("GGG"), mask);

					await tr.CommitAsync();
				}

				await DumpSubspace(db, db.Root);

			}
		}

		[Test]
		public async Task Test_Case_7()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				var location = db.Root;

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					var vX = Slice.FromFixedU32BE(0x55555555); // X
					var vY = Slice.FromFixedU32BE(0x66666666); // Y
					var vL1 = Slice.FromFixedU32BE(0x11111111); // Low
					var vL2 = Slice.FromFixedU32BE(0x22222222); // Low
					var vH2 = Slice.FromFixedU32BE(0xFFFFFFFF); // Hi
					var vH1 = Slice.FromFixedU32BE(0xEEEEEEEE); // Hi
					var vA = Slice.FromFixedU32BE(0xAAAAAAAA); // 10101010
					var vC = Slice.FromFixedU32BE(0xCCCCCCCC); // 11001100

					var commands = new[]
					{
						new { Op = "SET", Left = vX, Right = vY },
						new { Op = "ADD", Left = vX, Right = vY },
						new { Op = "AND", Left = vA, Right = vC },
						new { Op = "OR", Left = vA, Right = vC },
						new { Op = "XOR", Left = vA, Right = vC },
						new { Op = "MIN", Left = vL1, Right = vL2 },
						new { Op = "MAX", Left = vH1, Right = vH2 },
					};

					void Apply(IFdbTransaction t, string op, Slice k, Slice v)
					{
						switch (op)
						{
							case "SET":
								t.Set(k, v);
								break;
							case "ADD":
								t.AtomicAdd(k, v);
								break;
							case "AND":
								t.AtomicAnd(k, v);
								break;
							case "OR":
								t.AtomicOr(k, v);
								break;
							case "XOR":
								t.AtomicXor(k, v);
								break;
							case "MIN":
								t.AtomicMin(k, v);
								break;
							case "MAX":
								t.AtomicMax(k, v);
								break;
							default:
								Assert.Fail();
								break;
						}
					}

					for (int i = 0; i < commands.Length; i++)
					{
						for (int j = 0; j < commands.Length; j++)
						{
							var key = subspace.Encode(commands[i].Op + "_" + commands[j].Op);
							Log($"{i};{j} = {key}");
							Apply(tr, commands[i].Op, key, commands[i].Left);
							Apply(tr, commands[j].Op, key, commands[j].Right);
						}
					}

					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_8()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0000"), subspace.Encode("K9999\x00"));
					for (int i = 0; i < 1000; i++)
					{
						tr.Set(subspace.Encode("K" + i.ToString("D4")), Slice.FromFixedU32BE((uint)i));
					}
				}, this.Cancellation);

				for (int i = 0; i < 100; i++)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						var subspace = await db.Root.Resolve(tr);
						var res = await tr.GetAsync(subspace.Encode("K" + i.ToString("D4")));
						Dump(res);
					}
				}
			}
		}

		[Test]
		public async Task Test_Case_9()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				// clear everything
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0000"), subspace.Encode("K9999Z"));
				}, this.Cancellation);

				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Encode("K0123"), Text("V0123"));
				}, this.Cancellation);
				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Encode("K0789"), Text("V0789"));
				}, this.Cancellation);

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					await tr.GetValuesAsync(subspace.EncodeMany([ "K0123", "K0234", "K0456", "K0567", "K0789" ]));
				}

				// once more with feelings
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					await tr.GetValuesAsync(subspace.EncodeMany([ "K0123", "K0234", "K0456", "K0567", "K0789" ]));
				}
			}
		}

		[Test]
		public async Task Test_Case_10()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				// clear everything and write some values
				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0000"), subspace.Encode("K9999Z"));
					for (int i = 0; i < 100; i++)
					{
						tr.Set(subspace.Encode("K" + i.ToString("D4")), Text("V" + i.ToString("D4")));
					}
				}, this.Cancellation);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0010"), subspace.Encode("K0020"));
					tr.ClearRange(subspace.Encode("K0050"), subspace.Encode("K0060"));

					_ = await tr.GetRangeAsync(
						KeySelector.FirstGreaterOrEqual(subspace.Encode("K0000")),
						KeySelector.LastLessOrEqual(subspace.Encode("K9999")),
						FdbRangeOptions.WantAllReversed
					);

					//no commit
				}
			}
		}

		[Test]
		public async Task Test_Case_11()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				var location = db.Root;

				// clear everything and write some values
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0000"), subspace.Encode("K9999Z"));
					for (int i = 0; i < 100; i++)
					{
						tr.Set(subspace.Encode("K" + i.ToString("D4")), Text("V" + i.ToString("D4")));
					}
				}, this.Cancellation);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					tr.ClearRange(subspace.Encode("K0010"), subspace.Encode("K0020"));
					tr.ClearRange(subspace.Encode("K0050"), subspace.Encode("K0060"));
					tr.Set(subspace.Encode("K0021"), Slice.Empty);
					tr.Set(subspace.Encode("K0042"), Slice.Empty);

					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0005")));
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0010")));
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0015")));
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0022")));
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0049")));
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0050")));
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0055")));
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0061")));

					//no commit
				}

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					//tr.SetOption(FdbTransactionOption.ReadYourWritesDisable);
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0000"))); // equal=false, offset=1
					await tr.GetKeyAsync(KeySelector.FirstGreaterThan(subspace.Encode("K0011")));    // equal=true, offset=1
					await tr.GetKeyAsync(KeySelector.LastLessOrEqual(subspace.Encode("K0022")));	 // equal=true, offset=0
					await tr.GetKeyAsync(KeySelector.LastLessThan(subspace.Encode("K0033")));		 // equal=false, offset=0

					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("K0040")) + 1000); // equal=false, offset=7 ?
					await tr.GetKeyAsync(KeySelector.LastLessThan(subspace.Encode("K0050")) + 1000); // equal=false, offset=6 ?
				}
			}
		}

		[Test]
		public async Task Test_Case_12()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				var location = db.Root;

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					await tr.GetAsync(subspace.Encode("KGET"));
					tr.AddReadConflictRange(subspace.Encode("KRC0"), subspace.Encode("KRC0"));
					tr.AddWriteConflictRange(subspace.Encode("KWRITECONFLICT0"), subspace.Encode("KWRITECONFLICT1"));
					tr.Set(subspace.Encode("KWRITE"), Slice.Empty);
					await tr.CommitAsync();
				}

				// once more with feelings
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					tr.Options.WithReadYourWritesDisable();
					await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("KGETKEY")));
				}

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					tr.AddReadConflictRange(subspace.Encode("KRC0"), subspace.Encode("KRC1"));
					tr.Set(subspace.Encode("KWRITE"), Slice.Empty);
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_13()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				var location = db.Root;

				// clear everything and write some values
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0000"), subspace.Encode("K~~~~"));
					tr.Set(subspace.Encode("K000"), Text("BEGIN"));
					for (int i = 0; i < 5; i++)
					{
						tr.Set(subspace.Encode("K" + i + "A"), Text("V111"));
						tr.Set(subspace.Encode("K" + i + "B"), Text("V222"));
						tr.Set(subspace.Encode("K" + i + "C"), Text("V333"));
						tr.Set(subspace.Encode("K" + i + "D"), Text("V444"));
						tr.Set(subspace.Encode("K" + i + "E"), Text("V555"));
						tr.Set(subspace.Encode("K" + i + "F"), Text("V666"));
						tr.Set(subspace.Encode("K" + i + "G"), Text("V777"));
						tr.Set(subspace.Encode("K" + i + "H"), Text("V888"));
					}
					tr.Set(subspace.Encode("K~~~"), Text("END"));
				}, this.Cancellation);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					tr.Set(subspace.Encode("KZZZ"), Text("V999"));

					_ = await tr.GetRangeAsync(
						KeySelector.FirstGreaterOrEqual(subspace.Encode("K0B")),
						KeySelector.FirstGreaterOrEqual(subspace.Encode("K0G"))
					);

					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_14()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				var location = db.Root;

				// clear everything and write some values
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0000"), subspace.Encode("K~~~~"));
					tr.SetValues(Enumerable.Range(0, 100).Select(i => new KeyValuePair<Slice, Slice>(subspace.Encode("K" + i.ToString("D4")), Text("V" + i.ToString("D4")))));
					tr.Set(subspace.Encode("K~~~"), Text("END"));
				}, this.Cancellation);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					tr.ClearRange(subspace.Encode("K0042"), Text("K0069"));

					var r = await tr.GetRangeAsync(
						KeySelector.FirstGreaterOrEqual(subspace.Encode("K0040")),
						KeySelector.FirstGreaterOrEqual(subspace.Encode("K0080")),
						FdbRangeOptions.WantAll
					);
					// T 1
					// => GETRANGE( (< 'KAAA<00>' +1) .. (< LAST +1)
					Log($"Count={r.Count}, HasMore={r.HasMore}");
					foreach (var kvp in r)
					{
						Log($"{kvp.Key} = {kvp.Value}");
					}
				}
			}
		}

		[Test]
		public async Task Test_Case_15()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				var location = db.Root;

				// clear everything and write some values
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace.Encode("K0000"), subspace.Encode("K~~~~"));
					tr.Set(subspace.Encode("KAAA"), Text("V111"));
					tr.Set(subspace.Encode("KBBB"), Text("V222"));
					tr.Set(subspace.Encode("KCCC"), Text("V333"));
					tr.Set(subspace.Encode("K~~~"), Text("END"));
				}, this.Cancellation);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					// set a key, then read it, and check if it could conflict on it (it should not!)
					tr.Set(subspace.Encode("KBBB"), Text("V222b"));
					await tr.GetAsync(subspace.Encode("KBBB"));

					// read a key, then set it, and check if it could conflict on it (it should!)
					await tr.GetAsync(subspace.Encode("KCCC"));
					tr.Set(subspace.Encode("KCCC"), Text("V333b"));

					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_Case_16()
		{

			using (var db = await OpenTestPartitionAsync())
			{
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));

				// set the key
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Encode("KAAA"), Text("VALUE_AAA"));
					await tr.CommitAsync();
				}
				// set the key
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					await tr.GetAsync(subspace.Encode("KAAA"));
				}

				await Task.Delay(500);

				// first: concurrent trans, set only, no conflict
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					await Task.WhenAll(tr1.GetReadVersionAsync(), tr2.GetReadVersionAsync());

					var subspace1 = await db.Root.Resolve(tr1);
					var subspace2 = await db.Root.Resolve(tr2);

					tr1.Set(subspace1.Encode("KBBB"), Text("VALUE_BBB_111"));
					tr2.Set(subspace2.Encode("KCCC"), Text("VALUE_CCC_111"));
					var task1 = tr1.CommitAsync();
					var task2 = tr2.CommitAsync();

					await Task.WhenAll(task1, task2);
				}

				await Task.Delay(500);

				// first: concurrent trans, read + set, no conflict
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace1 = await db.Root.Resolve(tr1);
					var subspace2 = await db.Root.Resolve(tr2);

					await Task.WhenAll(
						tr1.GetAsync(subspace1.Encode("KAAA")),
						tr2.GetAsync(subspace2.Encode("KAAA"))
					);

					tr1.Set(subspace1.Encode("KBBB"), Text("VALUE_BBB_222"));
					tr2.Set(subspace2.Encode("KCCC"), Text("VALUE_CCC_222"));
					var task1 = tr1.CommitAsync();
					var task2 = tr2.CommitAsync();

					await Task.WhenAll(task1, task2);
				}

				await Task.Delay(500);

				// first: concurrent trans, read + set, conflict
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace1 = await db.Root.Resolve(tr1);
					var subspace2 = await db.Root.Resolve(tr2);

					await Task.WhenAll(
						tr1.GetAsync(subspace1.Encode("KCCC")),
						tr2.GetAsync(subspace2.Encode("KBBB"))
					);
					tr1.Set(subspace1.Encode("KBBB"), Text("VALUE_BBB_333"));
					tr2.Set(subspace2.Encode("KCCC"), Text("VALUE_CCC_333"));
					var task1 = tr1.CommitAsync();
					var task2 = tr2.CommitAsync();

					try
					{
						await Task.WhenAll(task1, task2);
					}
					catch (Exception e)
					{
						Log(e.Message);
					}
				}

				Log("DONE!!!");
			}
		}


		[Test][Ignore("This test requires the database to be stopped!")]
		public async Task Test_Case_17()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				//THIS TEST MUST BE PERFORMED WITH THE CLUSTER DOWN! (net stop fdbmonitor)

				// measured latencies:
				// "past_version": ALWAYS ~10 ms
				// "future_version": ALWAYS ~10 ms
				// "not_committed": start with 5, 10, 15, etc... but after 4 or 5, then transition into a random number between 0 and 1 sec

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					await tr.OnErrorAsync(FdbError.TransactionTooOld).ConfigureAwait(false);
					await tr.OnErrorAsync(FdbError.NotCommitted).ConfigureAwait(false);
				}


				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					for (int i = 0; i < 20; i++)
					{
						var code = i is > 1 and < 10 ? FdbError.TransactionTooOld : FdbError.CommitUnknownResult;
						var sw = Stopwatch.StartNew();
						await tr.OnErrorAsync(code).ConfigureAwait(false);
						sw.Stop();
						Log($"{sw.Elapsed.TotalMilliseconds:N3}");
					}
				}
			}
		}

	}

}
