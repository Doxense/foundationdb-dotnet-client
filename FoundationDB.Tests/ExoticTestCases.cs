#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

 namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture][Ignore("These tests are not meant to be run as part of a CI build")]
	public class ExoticTestCases : FdbTest
	{
		// This is a collection of specific test cases, used to trigger specific behaviors from the client
		// => THEY ARE NOT TESTING THE DATABASE ITSELF, ONLY USED AS TOOLS TO OBSERVE THE CHANGES TO THE DATABASE!

		[Test]
		public async void Test_Case_1()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var subspace = db.GlobalSpace;
				{
					var tr = db.BeginTransaction(this.Cancellation);
					tr.Set(subspace.Keys.Encode("AAA"), Slice.FromString("111"));
					tr.Set(subspace.Keys.Encode("BBB"), Slice.FromString("222"));
					tr.Set(subspace.Keys.Encode("CCC"), Slice.FromString("333"));
					tr.Set(subspace.Keys.Encode("DDD"), Slice.FromString("444"));
					tr.Set(subspace.Keys.Encode("EEE"), Slice.FromString("555"));
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async void Test_Case_2()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var subspace = db.GlobalSpace;
				{
					var tr = db.BeginTransaction(this.Cancellation);
					tr.ClearRange(subspace.Keys.Encode("AAA"), Slice.FromString("ZZZ"));
					tr.Set(subspace.Keys.Encode("AAA"), Slice.FromString("111"));
					tr.Set(subspace.Keys.Encode("BBB"), Slice.FromString("222"));
					tr.Set(subspace.Keys.Encode("CCC"), Slice.FromString("333"));
					tr.Set(subspace.Keys.Encode("DDD"), Slice.FromString("444"));
					tr.Set(subspace.Keys.Encode("EEE"), Slice.FromString("555"));
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async void Test_Case_3()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var subspace = db.GlobalSpace;
				{
					var tr = db.BeginTransaction(this.Cancellation);
					tr.ClearRange(subspace.Keys.Encode("AAA"), Slice.FromString("BBB"));
					tr.ClearRange(subspace.Keys.Encode("BBB"), Slice.FromString("CCC"));
					tr.ClearRange(subspace.Keys.Encode("CCC"), Slice.FromString("DDD"));
					// should be merged into a single AAA..DDD
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async void Test_Case_4()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var subspace = db.GlobalSpace;
				{
					var tr = db.BeginTransaction(this.Cancellation);
					//initial setup:
					// A: none
					// B: 0
					// C: 255
					// D: none
					// E: none
					tr.Set(subspace.Keys.Encode("BBB"), Slice.FromFixed32(0));
					tr.Set(subspace.Keys.Encode("CCC"), Slice.FromFixed32(255));

					// add 1 to everybody
					tr.AtomicAdd32(subspace.Keys.Encode("AAA"), 1);
					tr.AtomicAdd32(subspace.Keys.Encode("BBB"), -1);
					tr.AtomicAdd32(subspace.Keys.Encode("CCC"), 1U);
					tr.AtomicAdd64(subspace.Keys.Encode("DDD"), 1L);
					tr.AtomicAdd64(subspace.Keys.Encode("EEE"), 1UL);

					// overwrite DDD with a fixed value
					tr.Set(subspace.Keys.Encode("DDD"), Slice.FromFixed32(5));
					// double add on EEE
					tr.AtomicAdd32(subspace.Keys.Encode("EEE"), 1);

					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async void Test_Case_5()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var subspace = db.GlobalSpace;
				{
					var tr = db.BeginTransaction(this.Cancellation);
					tr.Set(subspace.Keys.Encode("AAA"), Slice.FromString("111"));
					tr.AtomicAdd(subspace.Keys.Encode("BBB"), Slice.FromString("222"));
					tr.AtomicAnd(subspace.Keys.Encode("CCC"), Slice.FromString("333"));
					tr.AtomicOr(subspace.Keys.Encode("DDD"), Slice.FromString("444"));
					tr.AtomicXor(subspace.Keys.Encode("EEE"), Slice.FromString("555"));
					tr.AtomicMax(subspace.Keys.Encode("FFF"), Slice.FromString("666"));
					tr.AtomicMin(subspace.Keys.Encode("GGG"), Slice.FromString("777"));
					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async void Test_Case_6()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var subspace = db.GlobalSpace;
				{
					var tr = db.BeginTransaction(this.Cancellation);

					tr.AtomicMax(subspace.Keys.Encode("MAXMAX1"), Slice.FromString("EEE"));
					tr.AtomicMax(subspace.Keys.Encode("MAXMAX1"), Slice.FromString("FFF"));

					tr.AtomicMax(subspace.Keys.Encode("MAXMAX2"), Slice.FromString("FFF"));
					tr.AtomicMax(subspace.Keys.Encode("MAXMAX2"), Slice.FromString("EEE"));

					tr.AtomicMin(subspace.Keys.Encode("MINMIN1"), Slice.FromString("111"));
					tr.AtomicMin(subspace.Keys.Encode("MINMIN1"), Slice.FromString("222"));

					tr.AtomicMin(subspace.Keys.Encode("MINMIN2"), Slice.FromString("222"));
					tr.AtomicMin(subspace.Keys.Encode("MINMIN2"), Slice.FromString("111"));

					await tr.CommitAsync();
				}
			}
		}

		[Test]
		public async void Test_Case_6b()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var subspace = db.GlobalSpace;

				Slice init = Slice.Repeat(0xCC, 9);
				Slice mask = Slice.Repeat(0xAA, 9);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.Set(subspace.Keys.Encode("AAA"), init);
					tr.Set(subspace.Keys.Encode("BBB"), init);
					tr.Set(subspace.Keys.Encode("CCC"), init);
					tr.Set(subspace.Keys.Encode("DDD"), init);
					tr.Set(subspace.Keys.Encode("EEE"), init);
					tr.Set(subspace.Keys.Encode("FFF"), init);
					tr.Set(subspace.Keys.Encode("GGG"), init);

					await tr.CommitAsync();
				}


				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.Set(subspace.Keys.Encode("AAA"), mask);
					tr.AtomicAdd(subspace.Keys.Encode("BBB"), mask);
					tr.AtomicAnd(subspace.Keys.Encode("CCC"), mask);
					tr.AtomicOr(subspace.Keys.Encode("DDD"), mask);
					tr.AtomicXor(subspace.Keys.Encode("EEE"), mask);
					tr.AtomicMin(subspace.Keys.Encode("FFF"), mask);
					tr.AtomicMax(subspace.Keys.Encode("GGG"), mask);

					await tr.CommitAsync();
				}

				await DumpSubspace(db, db.GlobalSpace);

			}
		}

		[Test]
		public async void Test_Case_7()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					using (var tr = db.BeginTransaction(this.Cancellation))
					{

						var vX = Slice.FromFixedU32BE(0x55555555); // X
						var vY = Slice.FromFixedU32BE(0x66666666); // Y
						var vL1 = Slice.FromFixedU32BE(0x11111111); // Low
						var vL2 = Slice.FromFixedU32BE(0x22222222); // Low
						var vH2 = Slice.FromFixedU32BE(0xFFFFFFFF); // Hi
						var vH1 = Slice.FromFixedU32BE(0xEEEEEEEE); // Hi
						var vA = Slice.FromFixedU32BE(0xAAAAAAAA); // 10101010
						var vC = Slice.FromFixedU32BE(0xCCCCCCCC); // 11001100

						var cmds = new[]
						{
							new { Op = "SET", Left = vX, Right = vY },
							new { Op = "ADD", Left = vX, Right = vY },
							new { Op = "AND", Left = vA, Right = vC },
							new { Op = "OR", Left = vA, Right = vC },
							new { Op = "XOR", Left = vA, Right = vC },
							new { Op = "MIN", Left = vL1, Right = vL2 },
							new { Op = "MAX", Left = vH1, Right = vH2 },
						};

						Action<IFdbTransaction, string, Slice, Slice> apply = (t, op, k, v) =>
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
						};

						for (int i = 0; i < cmds.Length; i++)
						{
							for (int j = 0; j < cmds.Length; j++)
							{
								Slice key = subspace.Keys.Encode(cmds[i].Op + "_" + cmds[j].Op);
								Log($"{i};{j} = {key}");
								apply(tr, cmds[i].Op, key, cmds[i].Left);
								apply(tr, cmds[j].Op, key, cmds[j].Right);
							}
						}

						await tr.CommitAsync();
					}
				}
			}
		}

		[Test]
		public async void Test_Case_8()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					await db.WriteAsync((tr) =>
					{
						tr.ClearRange(subspace.Keys.Encode("K0000"), subspace.Keys.Encode("K9999\x00"));
						for (int i = 0; i < 1000; i++)
						{
							tr.Set(subspace.Keys.Encode("K" + i.ToString("D4")), Slice.FromFixedU32BE((uint)i));
						}
					}, this.Cancellation);

					for (int i = 0; i < 100; i++)
					{
						using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
						{
							var res = await tr.GetAsync(subspace.Keys.Encode("K" + i.ToString("D4")));
							Dump(res);
						}
					}
				}
			}
		}

		[Test]
		public async void Test_Case_9()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					// clear everything
					await db.WriteAsync((tr) => tr.ClearRange(subspace.Keys.Encode("K0000"), subspace.Keys.Encode("K9999Z")), this.Cancellation);

					await db.WriteAsync(tr => tr.Set(subspace.Keys.Encode("K0123"), Slice.FromString("V0123")), this.Cancellation);
					await db.WriteAsync(tr => tr.Set(subspace.Keys.Encode("K0789"), Slice.FromString("V0789")), this.Cancellation);

					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(new[] {
							subspace.Keys.Encode("K0123"),
							subspace.Keys.Encode("K0234"),
							subspace.Keys.Encode("K0456"),
							subspace.Keys.Encode("K0567"),
							subspace.Keys.Encode("K0789")
						});
					}

					// once more with feelings
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(new[] {
							subspace.Keys.Encode("K0123"),
							subspace.Keys.Encode("K0234"),
							subspace.Keys.Encode("K0456"),
							subspace.Keys.Encode("K0567"),
							subspace.Keys.Encode("K0789")
						});
					}
				}
			}
		}

		[Test]
		public async void Test_Case_10()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					// clear everything and write some values
					await db.WriteAsync((tr) =>
					{
						tr.ClearRange(subspace.Keys.Encode("K0000"), subspace.Keys.Encode("K9999Z"));
						for (int i = 0; i < 100; i++)
						{
							tr.Set(subspace.Keys.Encode("K" + i.ToString("D4")), Slice.FromString("V" + i.ToString("D4")));
						}
					}, this.Cancellation);

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						tr.ClearRange(subspace.Keys.Encode("K0010"), subspace.Keys.Encode("K0020"));
						tr.ClearRange(subspace.Keys.Encode("K0050"), subspace.Keys.Encode("K0060"));

						var chunk = await tr.GetRangeAsync(
										KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0000")),
										KeySelector.LastLessOrEqual(subspace.Keys.Encode("K9999")),
										new FdbRangeOptions { Mode = FdbStreamingMode.WantAll, Reverse = true }
									);

						//no commit
					}

				}
			}
		}

		[Test]
		public async void Test_Case_11()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					// clear everything and write some values
					await db.WriteAsync((tr) =>
					{
						tr.ClearRange(subspace.Keys.Encode("K0000"), subspace.Keys.Encode("K9999Z"));
						for (int i = 0; i < 100; i++)
						{
							tr.Set(subspace.Keys.Encode("K" + i.ToString("D4")), Slice.FromString("V" + i.ToString("D4")));
						}
					}, this.Cancellation);

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						tr.ClearRange(subspace.Keys.Encode("K0010"), subspace.Keys.Encode("K0020"));
						tr.ClearRange(subspace.Keys.Encode("K0050"), subspace.Keys.Encode("K0060"));
						tr.Set(subspace.Keys.Encode("K0021"), Slice.Empty);
						tr.Set(subspace.Keys.Encode("K0042"), Slice.Empty);

						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0005")));
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0010")));
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0015")));
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0022")));
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0049")));
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0050")));
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0055")));
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0061")));

						//no commit
					}

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						//tr.SetOption(FdbTransactionOption.ReadYourWritesDisable);
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0000"))); // equal=false, offset=1
						await tr.GetKeyAsync(KeySelector.FirstGreaterThan(subspace.Keys.Encode("K0011")));    // equal=true, offset=1
						await tr.GetKeyAsync(KeySelector.LastLessOrEqual(subspace.Keys.Encode("K0022")));	 // equal=true, offset=0
						await tr.GetKeyAsync(KeySelector.LastLessThan(subspace.Keys.Encode("K0033")));		 // equal=false, offset=0

						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0040")) + 1000); // equal=false, offset=7 ?
						await tr.GetKeyAsync(KeySelector.LastLessThan(subspace.Keys.Encode("K0050")) + 1000); // equal=false, offset=6 ?
					}

				}
			}
		}

		[Test]
		public async void Test_Case_12()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						await tr.GetAsync(subspace.Keys.Encode("KGET"));
						tr.AddReadConflictRange(subspace.Keys.Encode("KRC0"), subspace.Keys.Encode("KRC0"));
						tr.AddWriteConflictRange(subspace.Keys.Encode("KWRITECONFLICT0"), subspace.Keys.Encode("KWRITECONFLICT1"));
						tr.Set(subspace.Keys.Encode("KWRITE"), Slice.Empty);
						await tr.CommitAsync();
					}

					// once more with feelings
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						tr.SetOption(FdbTransactionOption.ReadYourWritesDisable);
						await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("KGETKEY")));
					}

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						tr.AddReadConflictRange(subspace.Keys.Encode("KRC0"), subspace.Keys.Encode("KRC1"));
						tr.Set(subspace.Keys.Encode("KWRITE"), Slice.Empty);
						await tr.CommitAsync();
					}
				}
			}
		}

		[Test]
		public async void Test_Case_13()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					// clear everything and write some values
					await db.WriteAsync((tr) =>
					{
						tr.ClearRange(subspace.Keys.Encode("K0000"), subspace.Keys.Encode("K~~~~"));
						tr.Set(subspace.Keys.Encode("K000"), Slice.FromString("BEGIN"));
						for (int i = 0; i < 5; i++)
						{
							tr.Set(subspace.Keys.Encode("K" + i + "A"), Slice.FromString("V111"));
							tr.Set(subspace.Keys.Encode("K" + i + "B"), Slice.FromString("V222"));
							tr.Set(subspace.Keys.Encode("K" + i + "C"), Slice.FromString("V333"));
							tr.Set(subspace.Keys.Encode("K" + i + "D"), Slice.FromString("V444"));
							tr.Set(subspace.Keys.Encode("K" + i + "E"), Slice.FromString("V555"));
							tr.Set(subspace.Keys.Encode("K" + i + "F"), Slice.FromString("V666"));
							tr.Set(subspace.Keys.Encode("K" + i + "G"), Slice.FromString("V777"));
							tr.Set(subspace.Keys.Encode("K" + i + "H"), Slice.FromString("V888"));
						}
						tr.Set(subspace.Keys.Encode("K~~~"), Slice.FromString("END"));
					}, this.Cancellation);

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						tr.Set(subspace.Keys.Encode("KZZZ"), Slice.FromString("V999"));

						var r = await tr.GetRangeAsync(
									KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0B")),
									KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0G"))
								);

						await tr.CommitAsync();
					}
				}
			}
		}

		[Test]
		public async void Test_Case_14()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					// clear everything and write some values
					await db.WriteAsync((tr) =>
					{
						tr.ClearRange(subspace.Keys.Encode("K0000"), subspace.Keys.Encode("K~~~~"));
						tr.SetValues(Enumerable.Range(0, 100).Select(i => new KeyValuePair<Slice, Slice>(subspace.Keys.Encode("K" + i.ToString("D4")), Slice.FromString("V" + i.ToString("D4")))));
						tr.Set(subspace.Keys.Encode("K~~~"), Slice.FromString("END"));
					}, this.Cancellation);

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						tr.ClearRange(subspace.Keys.Encode("K0042"), Slice.FromString("K0069"));

						var r = await tr.GetRangeAsync(
									KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0040")),
									KeySelector.FirstGreaterOrEqual(subspace.Keys.Encode("K0080")),
									new FdbRangeOptions { Mode = FdbStreamingMode.WantAll }
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
		}

		[Test]
		public async void Test_Case_15()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					// clear everything and write some values
					await db.WriteAsync((tr) =>
					{
						tr.ClearRange(subspace.Keys.Encode("K0000"), subspace.Keys.Encode("K~~~~"));
						tr.Set(subspace.Keys.Encode("KAAA"), Slice.FromString("V111"));
						tr.Set(subspace.Keys.Encode("KBBB"), Slice.FromString("V222"));
						tr.Set(subspace.Keys.Encode("KCCC"), Slice.FromString("V333"));
						tr.Set(subspace.Keys.Encode("K~~~"), Slice.FromString("END"));
					}, this.Cancellation);

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						// set a key, then read it, and check if it could conflict on it (it should not!)
						tr.Set(subspace.Keys.Encode("KBBB"), Slice.FromString("V222b"));
						await tr.GetAsync(subspace.Keys.Encode("KBBB"));

						// read a key, then set it, and check if it could conflict on it (it should!)
						await tr.GetAsync(subspace.Keys.Encode("KCCC"));
						tr.Set(subspace.Keys.Encode("KCCC"), Slice.FromString("V333b"));

						await tr.CommitAsync();
					}

				}
			}
		}

		[Test]
		public async void Test_Case_16()
		{

			using (var zedb = await OpenTestDatabaseAsync())
			{
				var db = FoundationDB.Filters.Logging.FdbLoggingExtensions.Logged(zedb, (tr) => Log(tr.Log.GetTimingsReport(true)));
				{
					var subspace = db.GlobalSpace;

					Slice aaa = subspace.Keys.Encode("KAAA");
					Slice bbb = subspace.Keys.Encode("KBBB");
					Slice ccc = subspace.Keys.Encode("KCCC");

					//using (var tr = db.BeginTransaction(this.Cancellation))
					//{
					//	tr.ClearRange(subspace.Keys.Encode("K"), subspace.Keys.Encode("KZZZZZZZZZ"));
					//	await tr.CommitAsync();
					//}
					//return;

					// set the key
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						tr.Set(aaa, Slice.FromString("VALUE_AAA"));
						await tr.CommitAsync();
					}
					// set the key
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetAsync(aaa);
					}

					await Task.Delay(500);

					// first: concurrent trans, set only, no conflict
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						await Task.WhenAll(tr1.GetReadVersionAsync(), tr2.GetReadVersionAsync());

						tr1.Set(bbb, Slice.FromString("VALUE_BBB_111"));
						tr2.Set(ccc, Slice.FromString("VALUE_CCC_111"));
						var task1 = tr1.CommitAsync();
						var task2 = tr2.CommitAsync();

						await Task.WhenAll(task1, task2);
					}

					await Task.Delay(500);

					// first: concurrent trans, read + set, no conflict
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						await Task.WhenAll(tr1.GetAsync(aaa), tr2.GetAsync(aaa));

						tr1.Set(bbb, Slice.FromString("VALUE_BBB_222"));
						tr2.Set(ccc, Slice.FromString("VALUE_CCC_222"));
						var task1 = tr1.CommitAsync();
						var task2 = tr2.CommitAsync();

						await Task.WhenAll(task1, task2);
					}

					await Task.Delay(500);

					// first: concurrent trans, read + set, conflict
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						await Task.WhenAll(tr1.GetAsync(ccc), tr2.GetAsync(bbb));
						tr1.Set(bbb, Slice.FromString("VALUE_BBB_333"));
						tr2.Set(ccc, Slice.FromString("VALUE_CCC_333"));
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
		}


		[Test][Ignore("This test requires the database to be stopped!")]
		public async void Test_Case_17()
		{
			using (var zedb = await OpenTestDatabaseAsync())
			{
				//THIS TEST MUST BE PERFORMED WITH THE CLUSTER DOWN! (net stop fdbmonitor)

				// measured latencies:
				// "past_version": ALWAYS ~10 ms
				// "future_version": ALWAYS ~10 ms
				// "not_committed": start with 5, 10, 15, etc... but after 4 or 5, then transition into a random number between 0 and 1 sec

				using (var tr = zedb.BeginReadOnlyTransaction(this.Cancellation))
				{
					await tr.OnErrorAsync(FdbError.PastVersion).ConfigureAwait(false);
					await tr.OnErrorAsync(FdbError.NotCommitted).ConfigureAwait(false);
				}


				using (var tr = zedb.BeginReadOnlyTransaction(this.Cancellation))
				{
					for (int i = 0; i < 20; i++)
					{
						//tr.Timeout = 500;
						//try
						//{
						//	await tr.GetAsync(Slice.FromAscii("SomeRandomKey"));
						//	Assert.Fail("The database must be offline !");
						//}
						//catch(FdbException e)
						{
							var code = i > 1 && i < 10 ? FdbError.PastVersion : FdbError.CommitUnknownResult;
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
}
