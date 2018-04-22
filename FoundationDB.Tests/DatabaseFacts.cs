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

// ReSharper disable AccessToDisposedClosure
namespace FoundationDB.Client.Tests
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using NUnit.Framework;

	[TestFixture]
	public class DatabaseFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Open_Database()
		{
			//README: if your default cluster is remote, you need to be connected to the netword, or it will fail.

			// the hard way
			using (var cluster = await Fdb.CreateClusterAsync(null, this.Cancellation))
			{
				Assert.That(cluster, Is.Not.Null);
				Assert.That(cluster.Path, Is.Null);

				using (var db = await cluster.OpenDatabaseAsync("DB", KeySubspace.Empty, false, this.Cancellation))
				{
					Assert.That(db, Is.Not.Null, "Should return a valid object");
					Assert.That(db.Name, Is.EqualTo("DB"), "FdbDatabase.Name should match");
					Assert.That(db.Cluster, Is.SameAs(cluster), "FdbDatabase.Cluster should point to the parent cluster");
				}
			}

			// the easy way		
			using(var db = await Fdb.OpenAsync())
			{
				Assert.That(db, Is.Not.Null);
				Assert.That(db.Name, Is.EqualTo("DB"));
				Assert.That(db.Cluster, Is.Not.Null);
				Assert.That(db.Cluster.Path, Is.Null);
			}
		}

		[Test]
		public async Task Test_Open_Database_With_Cancelled_Token_Should_Fail()
		{
			using (var cts = new CancellationTokenSource())
			{
				using (var cluster = await Fdb.CreateClusterAsync(cts.Token))
				{
					cts.Cancel();
					Assert.That(async () => await cluster.OpenDatabaseAsync("DB", KeySubspace.Empty, false, cts.Token), Throws.InstanceOf<OperationCanceledException>());
				}
			}
		}

		[Test]
		public async Task Test_Open_Database_With_Invalid_Name_Should_Fail()
		{
			// As of 1.0, the only accepted database name is "DB".
			// Any other name should fail with "InvalidDatabaseName"

			// note: Don't forget to update this test if in the future if the API allows for other names !

			using (var cluster = await Fdb.CreateClusterAsync(this.Cancellation))
			{
				await TestHelpers.AssertThrowsFdbErrorAsync(() => cluster.OpenDatabaseAsync("SomeOtherName", KeySubspace.Empty, false, this.Cancellation), FdbError.InvalidDatabaseName, "Passing anything other then 'DB' should fail");
			}

			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(null, "SomeOtherName"), FdbError.InvalidDatabaseName, "Passing anything other then 'DB' should fail");			

			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(null, "SomeOtherName", KeySubspace.Empty), FdbError.InvalidDatabaseName, "Passing anything other then 'DB' should fail");			
		}

		[Test]
		public async Task Test_Open_Or_CreateCluster_With_Invalid_ClusterFile_Path_Should_Fail()
		{
			// Missing/Invalid cluster files should fail with "NoClusterFileFound"

			// file not found
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.CreateClusterAsync(@".\file_not_found.cluster", this.Cancellation), FdbError.NoClusterFileFound, "Should fail if cluster file is missing");
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(@".\file_not_found.cluster", "DB", this.Cancellation), FdbError.NoClusterFileFound, "Should fail if cluster file is missing");

			// unreachable path
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.CreateClusterAsync(@"C:\..\..\fdb.cluster", this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(@"C:\..\..\fdb.cluster", "DB", this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");

			// malformed path
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.CreateClusterAsync(@"FOO:\invalid$path!/fdb.cluster", this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(@"FOO:\invalid$path!/fdb.cluster", "DB", this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");
		}

		[Test]
		public async Task Test_Open_Or_CreateCluster_With_Corrupted_ClusterFile_Should_Fail()
		{
			// Using a corrupted cluster file should fail with "ConnectionStringInvalid"

			// write some random bytes into a cluster file
			string path = System.IO.Path.GetTempFileName();
			try
			{
				var rnd = new Random();
				var bytes = new byte[128];
				rnd.NextBytes(bytes);
				System.IO.File.WriteAllBytes(path, bytes);

				await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.CreateClusterAsync(path, this.Cancellation), FdbError.ConnectionStringInvalid, "Should fail if file is corrupted");
				await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(path, "DB"), FdbError.ConnectionStringInvalid, "Should fail if file is corrupted");
			}
			finally
			{
				System.IO.File.Delete(path);
			}

		}

		[Test]
		public async Task Test_Can_Open_Local_Database()
		{
			//README: if your test database is remote, and you don't have FDB running locally, this test will fail and you should ignore this one.

			using (var db = await Fdb.OpenAsync(this.Cancellation))
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

			using (var db = await OpenTestDatabaseAsync())
			{
				Assert.That(db, Is.Not.Null, "Should return a valid database");
				Assert.That(db.Cluster, Is.Not.Null, "FdbDatabase should have its own Cluster instance");
				Assert.That(db.Cluster.Path, Is.Null, "Cluster path should be null (default)");
			}
		}

		[Test]
		public async Task Test_FdbDatabase_Key_Validation()
		{
			using(var db = await Fdb.OpenAsync())
			{
				// IsKeyValid
				Assert.That(db.IsKeyValid(Slice.Nil), Is.False, "Null key is invalid");
				Assert.That(db.IsKeyValid(Slice.Empty), Is.True, "Empty key is allowed");
				Assert.That(db.IsKeyValid(Slice.FromString("hello")), Is.True);
				Assert.That(db.IsKeyValid(Slice.Create(Fdb.MaxKeySize + 1)), Is.False, "Key is too large");
				Assert.That(db.IsKeyValid(Fdb.System.Coordinators), Is.True, "System keys are valid");

				// EnsureKeyIsValid
				Assert.That(() => db.EnsureKeyIsValid(Slice.Nil), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => db.EnsureKeyIsValid(Slice.Empty), Throws.Nothing);
				Assert.That(() => db.EnsureKeyIsValid(Slice.FromString("hello")), Throws.Nothing);
				Assert.That(() => db.EnsureKeyIsValid(Slice.Create(Fdb.MaxKeySize + 1)), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => db.EnsureKeyIsValid(Fdb.System.Coordinators), Throws.Nothing);

				// EnsureKeyIsValid ref
				Assert.That(() => { Slice key = Slice.Nil; db.EnsureKeyIsValid(ref key); }, Throws.InstanceOf<ArgumentException>());
				Assert.That(() => { Slice key = Slice.Empty; db.EnsureKeyIsValid(ref key); }, Throws.Nothing);
				Assert.That(() => { Slice key = Slice.FromString("hello"); db.EnsureKeyIsValid(ref key); }, Throws.Nothing);
				Assert.That(() => { Slice key = Slice.Create(Fdb.MaxKeySize + 1); db.EnsureKeyIsValid(ref key); }, Throws.InstanceOf<ArgumentException>());
				Assert.That(() => { Slice key = Fdb.System.Coordinators; db.EnsureKeyIsValid(ref key); }, Throws.Nothing);

			}
		}

		[Test]
		public async Task Test_Can_Get_Coordinators()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var coordinators = await Fdb.System.GetCoordinatorsAsync(db, this.Cancellation);
				Assert.That(coordinators, Is.Not.Null);
				Log("raw : " + coordinators.RawValue);
				Log("id  : "+ coordinators.Id);
				Log("desc:" + coordinators.Description);
				Log("coordinators:");
				foreach (var x in coordinators.Coordinators)
				{
					Log($"-  {x.Address}:{x.Port}{(x.Tls ? " (TLS)" : "")}");
				}
				Assert.That(coordinators.Description, Is.Not.Null.Or.Empty); //note: it should be a long numerical string, but it changes for each installation
				Assert.That(coordinators.Id, Is.Not.Null.And.Length.GreaterThan(0));
				Assert.That(coordinators.Coordinators, Is.Not.Null.And.Length.GreaterThan(0));

				Assert.That(coordinators.Coordinators[0], Is.Not.Null);
				Assert.That(coordinators.Coordinators[0].Port, Is.GreaterThanOrEqualTo(4500).And.LessThanOrEqualTo(4510)); //HACKHACK: may not work everywhere !

				//TODO: how can we check that it is correct?
				Log("Coordinators: {0}", coordinators);
			}
		}

		[Test]
		public async Task Test_Can_Get_Storage_Engine()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				string mode = await Fdb.System.GetStorageEngineModeAsync(db, this.Cancellation);
				Log("Storage engine: {0}", mode);
				Assert.That(mode, Is.Not.Null);
				Assert.That(mode, Is.EqualTo("ssd").Or.EqualTo("memory"));

				// in order to verify the value, we need to check ourselves by reading from the cluster config
				Slice actual;
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation).WithReadAccessToSystemKeys())
				{
					actual = await tr.GetAsync(Slice.FromByteString("\xFF/conf/storage_engine"));
				}

				if (mode == "ssd")
				{ // ssd = '0'
					Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("0")));
				}
				else
				{ // memory = '1'
					Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("1")));
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_System_Status()
		{

			using (var db = await OpenTestDatabaseAsync())
			{
				var status = await Fdb.System.GetStatusAsync(db, this.Cancellation);
				Assert.That(status, Is.Not.Null);

				Assert.That(status.Client, Is.Not.Null);
				Assert.That(status.Client.Messages, Is.Not.Null);

				Assert.That(status.Cluster, Is.Not.Null);
				Assert.That(status.Cluster.Messages, Is.Not.Null);
				Assert.That(status.Cluster.Data, Is.Not.Null);
				Assert.That(status.Cluster.Qos, Is.Not.Null);
				Assert.That(status.Cluster.Workload, Is.Not.Null);
			}

		}

		[Test]
		public async Task Test_Can_Open_Database_With_Non_Empty_GlobalSpace()
		{
			// using a tuple prefix
			using (var db = await Fdb.OpenAsync(null, "DB", KeySubspace.Create(TuPack.EncodeKey("test")), false, this.Cancellation))
			{
				Assert.That(db, Is.Not.Null);
				Assert.That(db.GlobalSpace, Is.Not.Null);
				Assert.That(db.GlobalSpace.GetPrefix().ToString(), Is.EqualTo("<02>test<00>"));

				var subspace = db.Partition.ByKey("hello");
				Assert.That(subspace.GetPrefix().ToString(), Is.EqualTo("<02>test<00><02>hello<00>"));

				// keys inside the global space are valid
				Assert.That(db.IsKeyValid(TuPack.EncodeKey("test", 123)), Is.True);

				// keys outside the global space are invalid
				Assert.That(db.IsKeyValid(Slice.FromByte(42)), Is.False);
			}

			// using a random binary prefix
			using (var db = await Fdb.OpenAsync(null, "DB", new KeySubspace(new byte[] { 42, 255, 0, 90 }.AsSlice()), false, this.Cancellation))
			{
				Assert.That(db, Is.Not.Null);
				Assert.That(db.GlobalSpace, Is.Not.Null);
				Assert.That(db.GlobalSpace.GetPrefix().ToString(), Is.EqualTo("*<FF><00>Z"));

				var subspace = db.Partition.ByKey("hello");
				Assert.That(subspace.GetPrefix().ToString(), Is.EqualTo("*<FF><00>Z<02>hello<00>"));

				// keys inside the global space are valid
				Assert.That(db.IsKeyValid(Slice.Unescape("*<FF><00>Z123")), Is.True);

				// keys outside the global space are invalid
				Assert.That(db.IsKeyValid(Slice.FromByte(123)), Is.False);
				Assert.That(db.IsKeyValid(Slice.Unescape("*<FF>")), Is.False);

			}

		}

		[Test]
		public async Task Test_Can_Change_Location_Cache_Size()
		{
			// New in Beta2

			using (var db = await OpenTestDatabaseAsync())
			{

				//TODO: how can we test that it is successfull ?

				db.SetLocationCacheSize(1000);
				db.SetLocationCacheSize(0); // does this disable location cache ?
				db.SetLocationCacheSize(9001);

				// should reject negative numbers
				Assert.That(() => db.SetLocationCacheSize(-123), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.InvalidOptionValue).And.Property("Success").False);
			}
		}

		[Test]
		public async Task Test_Database_Instance_Should_Have_Default_Root_Directory()
		{
			//TODO: move this into a dedicated test class for partitions

			using (var db = await OpenTestPartitionAsync())
			{
				Assert.That(db, Is.Not.Null);
				Assert.That(db.Name, Is.EqualTo(TestHelpers.TestDbName));

				var directory = db.Directory;
				Assert.That(directory, Is.Not.Null);
				Assert.That(directory.Path, Is.Not.Null);

				var dl = directory.DirectoryLayer;
				Assert.That(dl, Is.Not.Null);
				Assert.That(dl.ContentSubspace, Is.Not.Null);
				Assert.That(dl.ContentSubspace.GetPrefix(), Is.EqualTo(db.GlobalSpace.GetPrefix()));
				Assert.That(dl.NodeSubspace, Is.Not.Null);
				Assert.That(dl.NodeSubspace.GetPrefix(), Is.EqualTo(db.GlobalSpace.ConcatKey(Slice.FromByte(254))));
				Assert.That(db.GlobalSpace.Contains(dl.ContentSubspace.GetPrefix()), Is.True);
				Assert.That(db.GlobalSpace.Contains(dl.NodeSubspace.GetPrefix()), Is.True);

			}
		}

		[Test]
		public async Task Test_Check_Timeout_On_Non_Existing_Database()
		{

			string clusterPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "notfound.cluster");
			File.WriteAllText(clusterPath, "local:thisClusterShouldNotExist@127.0.0.1:4566");

			using (var db = await Fdb.OpenAsync(clusterPath, "DB"))
			{
				bool exists = false;
				var err = FdbError.Success;
				try
				{
					using(var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						tr.Timeout = 250; // ms
						Log("check ...");
						await tr.GetAsync(db.GlobalSpace.GetPrefix());
						Log("Uhoh ...?");
						exists = true;
					}
				}
				catch(FdbException e)
				{
					err = e.Code;
				}

				Assert.That(exists, Is.False);
				Assert.That(err, Is.EqualTo(FdbError.TransactionTimedOut));
			}

		}

	}
}
