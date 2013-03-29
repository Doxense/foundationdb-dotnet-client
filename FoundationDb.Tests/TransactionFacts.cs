using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FoundationDb.Client;
using System.Threading.Tasks;
using System.Threading;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class TransactionFacts
	{

		[Test]
		public async Task Test_Can_Create_Transaction()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
					Assert.That(tr.Database, Is.SameAs(db), "Transaction should reference the parent Database");
				}
			}
		}

		[Test]
		public async Task Test_Commiting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!
				}
			}
		}

		[Test]
		public async Task Test_Resetting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!
				}
			}
		}


	}
}
