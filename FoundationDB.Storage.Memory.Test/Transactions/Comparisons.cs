#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Storage.Memory.Tests;
	using NUnit.Framework;
	using System;
	using System.Threading.Tasks;

	[TestFixture]
	public class Comparisons : FdbTest
	{
		// Compare the behavior of the MemoryDB against a FoundationDB database

		private async Task Scenario1(IFdbTransaction tr)
		{
			tr.Set(Slice.FromAscii("hello"), Slice.FromAscii("world!"));
			tr.Clear(Slice.FromAscii("removed"));
			var result = await tr.GetAsync(Slice.FromAscii("narf"));
		}

		private Task Scenario2(IFdbTransaction tr)
		{
			var location = FdbSubspace.CreateDynamic(Slice.FromAscii("TEST"));
			tr.ClearRange(FdbKeyRange.StartsWith(location.Key));
			for (int i = 0; i < 10; i++)
			{
				tr.Set(location.Keys.Encode(i), Slice.FromString("value of " + i));
			}
			return Task.FromResult<object>(null);
		}

		private Task Scenario3(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			tr.Set(location.Key + (byte)'a', Slice.FromAscii("A"));
			tr.AtomicAdd(location.Key + (byte)'k', Slice.FromFixed32(1));
			tr.Set(location.Key + (byte)'z', Slice.FromAscii("C"));
			tr.ClearRange(location.Key + (byte)'a', location.Key + (byte)'k');
			tr.ClearRange(location.Key + (byte)'k', location.Key + (byte)'z');
			return Task.FromResult<object>(null);
		}

		private Task Scenario4(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			//tr.Set(location.Key, Slice.FromString("NARF"));
			//tr.AtomicAdd(location.Key, Slice.FromFixedU32(1));
			tr.AtomicAnd(location.Key, Slice.FromFixedU32(7));
			tr.AtomicXor(location.Key, Slice.FromFixedU32(3));
			tr.AtomicXor(location.Key, Slice.FromFixedU32(15));
			return Task.FromResult<object>(null);
		}

		private async Task Scenario5(IFdbTransaction tr)
		{
			var location = FdbSubspace.CreateDynamic(Slice.FromAscii("TEST"));

			//tr.Set(location.Pack(42), Slice.FromString("42"));
			//tr.Set(location.Pack(50), Slice.FromString("50"));
			//tr.Set(location.Pack(60), Slice.FromString("60"));

			var x = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(location.Keys.Encode(49)));
			Console.WriteLine(x);

			tr.Set(location.Keys.Encode("FOO"), Slice.FromString("BAR"));

		}

		private async Task Scenario6(IFdbTransaction tr)
		{
			var location = FdbSubspace.CreateDynamic(Slice.FromAscii("TEST"));

			tr.AtomicAdd(location.Keys.Encode("ATOMIC"), Slice.FromFixed32(0x55555555));

			var x = await tr.GetAsync(location.Keys.Encode("ATOMIC"));
			Console.WriteLine(x.ToInt32().ToString("x"));
		}

		[Test][Ignore]
		public async Task Test_Compare_Implementations()
		{
			for (int mode = 1; mode <= 6; mode++)
			{

				Console.WriteLine("#### SCENARIO " + mode + " ####");

				using (var db = await Fdb.OpenAsync(this.Cancellation))
				{
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						await tr.GetReadVersionAsync();

						switch (mode)
						{
							case 1: await Scenario1(tr); break;
							case 2: await Scenario2(tr); break;
							case 3: await Scenario3(tr); break;
							case 4: await Scenario4(tr); break;
							case 5: await Scenario5(tr); break;
							case 6: await Scenario6(tr); break;
						}

						await tr.CommitAsync();
					}
				}

				using (var db = MemoryDatabase.CreateNew("DB"))
				{
					using (var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation))
					{
						await tr.GetReadVersionAsync();

						switch (mode)
						{
							case 1: await Scenario1(tr); break;
							case 2: await Scenario2(tr); break;
							case 3: await Scenario3(tr); break;
							case 4: await Scenario4(tr); break;
							case 5: await Scenario5(tr); break;
							case 6: await Scenario6(tr); break;
						}

						await tr.CommitAsync();
					}

					db.Debug_Dump();
				}
			}
		}

	}
}
