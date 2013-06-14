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
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Client.Tests
{
	using FoundationDb.Client;
	using FoundationDb.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class DatabaseFacts
	{

		[Test]
		public async Task Test_Can_Open_Database()
		{
			//README: if your test db is remote and you don't have a local running fdb instance, this test will fail and you should ignore this.

			using (var cluster = await Fdb.OpenLocalClusterAsync())
			{
				Assert.That(cluster, Is.Not.Null);
				Assert.That(cluster.Path, Is.Null);

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
			// As of Beta2, the only accepted database name is "DB"
			// The Beta2 API silently fails (deadlock) with any other name, so make sure that OpenDatabaseAsync does protect us against that!

			// Don't forget to update this test if in the future the API allows for other names !

			using (var cluster = await Fdb.OpenLocalClusterAsync())
			{
				Assert.That(() => cluster.OpenDatabaseAsync("SomeOtherName").GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
			}
		}

		[Test]
		public async Task Test_Can_Open_Local_Database()
		{
			//README: if your test database is remote, and you don't have FDB running locally, this test will fail and you should ignore this one.

			using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				Assert.That(db, Is.Not.Null, "Should return a valid database");
				Assert.That(db.Cluster, Is.Not.Null, "FdbDatabase should have its own Cluster instance");
				Assert.That(db.Cluster.Path, Is.Null, "Cluster path should be null (default)");
			}
		}

		[Test]
		public async Task Test_Can_Open_Test_Database()
		{
			// note: may be different than local db !

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				Assert.That(db, Is.Not.Null, "Should return a valid database");
				Assert.That(db.Cluster, Is.Not.Null, "FdbDatabase should have its own Cluster instance");
				Assert.That(db.Cluster.Path, Is.Null, "Cluster path should be null (default)");
			}
		}

		[Test]
		public async Task Test_Can_Get_Coordinators()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var coordinators = await db.GetCoordinatorsAsync();
				Assert.That(coordinators, Is.StringStarting("local:"));

				//TODO: how can we check that it is correct?
				Console.WriteLine("Coordinators: " + coordinators);
			}
		}

		[Test]
		public async Task Test_Can_Change_Restricted_Key_Space()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				Assert.That(db.KeySpace.Begin, Is.EqualTo(Slice.Nil));
				Assert.That(db.KeySpace.End, Is.EqualTo(Slice.Nil));

				// can set min and max
				db.RestrictKeySpace(
					Slice.FromAscii("alpha"),
					Slice.FromAscii("omega")
				);
				Assert.That(db.KeySpace.Begin.ToAscii(), Is.EqualTo("alpha"));
				Assert.That(db.KeySpace.End.ToAscii(), Is.EqualTo("omega"));

				// can use a tuple as prefix
				db.RestrictKeySpace(
					FdbTuple.Create("prefix")
				);
				Assert.That(db.KeySpace.Begin.ToString(), Is.EqualTo("<02>prefix<00><00>"));
				Assert.That(db.KeySpace.End.ToString(), Is.EqualTo("<02>prefix<00><FF>"));

				// can use a slice as a prefix
				db.RestrictKeySpace(
					Slice.FromHexa("BEEF")
				);
				Assert.That(db.KeySpace.Begin.ToString(), Is.EqualTo("<BE><EF><00>"));
				Assert.That(db.KeySpace.End.ToString(), Is.EqualTo("<BE><EF><FF>"));

				// can directlry specify a range
				db.RestrictKeySpace(
					FdbKeyRange.FromPrefix(Slice.Create(new byte[] { 1, 2, 3 }))
				);
				Assert.That(db.KeySpace.Begin.ToString(), Is.EqualTo("<01><02><03><00>"));
				Assert.That(db.KeySpace.End.ToString(), Is.EqualTo("<01><02><03><FF>"));

				// throws if bounds are reversed
				Assert.Throws<ArgumentException>(() => db.RestrictKeySpace(Slice.FromAscii("Z"), Slice.FromAscii("A")));
			}		
		}

		[Test]
		public async Task Test_Can_Change_Location_Cache_Size()
		{
			// New in Beta2

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				//TODO: how can we test that it is successfull ?

				db.SetLocationCacheSize(1000);
				db.SetLocationCacheSize(0); // does this disable location cache ?
				db.SetLocationCacheSize(9001);

				// should reject negative numbers
				Assert.That(() => db.SetLocationCacheSize(-123), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.InvalidOptionValue).And.Property("Success").False);
			}
		}
	}
}
