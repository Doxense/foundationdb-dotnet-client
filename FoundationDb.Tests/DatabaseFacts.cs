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
	public class DatabaseFacts
	{

		[Test]
		public async Task Test_Can_Open_Database()
		{
			using (var cluster = await Fdb.OpenLocalClusterAsync())
			{
				using (var db = await cluster.OpenDatabaseAsync("DB"))
				{
					Assert.That(db, Is.Not.Null, "Should return a valid object");
					Assert.That(db.Name, Is.EqualTo("DB"), "FdbDatabase.Name should match");
					Assert.That(db.Cluster, Is.SameAs(cluster), "FdbDatabase.Cluster should point to the parent cluster");
				}
			}
		}

		[Test]
		public async Task Test_Open_Database_With_Cancelled_Token_Should_Fail()
		{
			using (var cts = new CancellationTokenSource())
			{
				using (var cluster = await Fdb.OpenLocalClusterAsync(cts.Token))
				{
					cts.Cancel();
					Assert.Throws<OperationCanceledException>(() => cluster.OpenDatabaseAsync("DB", cts.Token).GetAwaiter().GetResult());
				}
			}
		}

		[Test]
		public async Task Test_Open_Database_With_Invalid_Name_Should_Fail()
		{
			// As of Beta1, the only accepted database name is "DB"
			// The Beta1 API silently fails (deadlock) with any other name, so make sure that OpenDatabaseAsync does protect us against that!

			using (var cluster = await Fdb.OpenLocalClusterAsync())
			{
				Assert.Throws<InvalidOperationException>(() => cluster.OpenDatabaseAsync("SomeOtherName").GetAwaiter().GetResult());
			}
		}

	}
}
