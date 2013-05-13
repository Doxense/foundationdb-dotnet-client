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

using FoundationDb.Client;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class DatabaseFacts
	{
		[TestFixtureSetUp]
		public void Setup()
		{
			//TODO: cleanup ?
		}

		[TestFixtureTearDown]
		public void Teardown()
		{
			Fdb.Stop();
		}

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
