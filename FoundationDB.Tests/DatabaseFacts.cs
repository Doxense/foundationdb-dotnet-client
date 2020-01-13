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
			//README: if your default cluster is remote, you need to be connected to the network, or it will fail.

			// the easy way
			using(var db = await Fdb.OpenAsync(this.Cancellation))
			{
				Assert.That(db, Is.Not.Null);
				Assert.That(db.ClusterFile, Is.Null, ".ClusterFile");
				Assert.That(db.Root, Is.Not.Null, ".Root");
				Assert.That(db.DirectoryLayer, Is.Not.Null.And.SameAs(db.Root.Directory), ".DirectoryLayer");
				Assert.That(db.IsReadOnly, Is.False, ".IsReadOnly");
			}
		}

		[Test]
		public void Test_Open_Database_With_Cancelled_Token_Should_Fail()
		{
			using (var cts = new CancellationTokenSource())
			{
				cts.Cancel();
				Assert.That(async () => await Fdb.OpenAsync(cts.Token), Throws.InstanceOf<OperationCanceledException>());
			}
		}

		[Test]
		public async Task Test_Open_Or_CreateCluster_With_Invalid_ClusterFile_Path_Should_Fail()
		{
			// Missing/Invalid cluster files should fail with "NoClusterFileFound"

			// file not found
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = @".\file_not_found.cluster" }, this.Cancellation), FdbError.NoClusterFileFound, "Should fail if cluster file is missing");

			// unreachable path
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = @"C:\..\..\fdb.cluster" }, this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");

			// malformed path
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = @"FOO:\invalid$path!/fdb.cluster" }, this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");
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

				await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = path }, this.Cancellation), FdbError.ConnectionStringInvalid, "Should fail if file is corrupted");
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
				Assert.That(db.ClusterFile, Is.Null, "Cluster path should be null (default)");
			}
		}

		[Test]
		public async Task Test_Can_Open_Test_Database()
		{
			// note: may be different than local db !

			using (var db = await OpenTestDatabaseAsync())
			{
				Assert.That(db, Is.Not.Null, "Should return a valid database");
				Assert.That(db.ClusterFile, Is.Null, "Cluster path should be null (default)");
				Assert.That(db.Root, Is.Not.Null, ".Root");
				Assert.That(db.Root.Path, Is.EqualTo(FdbDirectoryPath.Empty), ".Root");
				Assert.That(db.DirectoryLayer, Is.Not.Null, ".DirectoryLayer");
			}
		}

		[Test]
		public void Test_FdbDatabase_Key_Validation()
		{
			// IsKeyValid
			Assert.That(FdbKey.IsKeyValid(Slice.Nil), Is.False, "Null key is invalid");
			Assert.That(FdbKey.IsKeyValid(Slice.Empty), Is.True, "Empty key is allowed");
			Assert.That(FdbKey.IsKeyValid(Slice.FromString("hello")), Is.True);
			Assert.That(FdbKey.IsKeyValid(Slice.Zero(Fdb.MaxKeySize + 1)), Is.False, "Key is too large");
			Assert.That(FdbKey.IsKeyValid(Fdb.System.Coordinators), Is.True, "System keys are valid");

			// EnsureKeyIsValid
			Assert.That(() => FdbKey.EnsureKeyIsValid(Slice.Nil), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => FdbKey.EnsureKeyIsValid(Slice.Empty), Throws.Nothing);
			Assert.That(() => FdbKey.EnsureKeyIsValid(Slice.FromString("hello")), Throws.Nothing);
			Assert.That(() => FdbKey.EnsureKeyIsValid(Slice.Zero(Fdb.MaxKeySize + 1)), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => FdbKey.EnsureKeyIsValid(Fdb.System.Coordinators), Throws.Nothing);
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
				Log($"Coordinators: {coordinators}");
			}
		}

		[Test]
		public async Task Test_Can_Get_Storage_Engine()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				string mode = await Fdb.System.GetStorageEngineModeAsync(db, this.Cancellation);
				Log($"Storage engine: {mode}");
				Assert.That(mode, Is.Not.Null);
				Assert.That(mode, Is.EqualTo("ssd").Or.EqualTo("memory").Or.EqualTo("ssd-2"));

				// in order to verify the value, we need to check ourselves by reading from the cluster config
				Slice actual;
				using (var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
				{
					tr.WithReadAccessToSystemKeys();
					actual = await tr.GetAsync(Slice.FromByteString("\xFF/conf/storage_engine"));
				}

				if (mode == "ssd")
				{ // ssd = '0'
					Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("0")));
				}
				else if (mode == "ssd-2")
				{ // ssd-2 = '2'
					Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("2")));
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
		public async Task Test_Can_Change_Location_Cache_Size()
		{
			// New in Beta2

			using (var db = await OpenTestDatabaseAsync())
			{

				//TODO: how can we test that it is successful ?

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

				Assert.That(db.Root, Is.Not.Null);
				Assert.That(db.Root.Path, Is.Not.EqualTo(FdbDirectoryPath.Empty));

				var dl = db.DirectoryLayer;
				Assert.That(dl, Is.Not.Null);
				Assert.That(dl.Content, Is.Not.Null);
				Assert.That(dl.Content, Is.EqualTo(SubspaceLocation.Empty), "Root DL should be located at the top");

				using (var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
				{
					var root = await db.Root.Resolve(tr);
					Assert.That(root, Is.Not.Null);
					Assert.That(root.Path, Is.EqualTo(db.Root.Path));
					Assert.That(root.DirectoryLayer, Is.SameAs(dl));
				}
			}
		}

		[Test]
		public async Task Test_Check_Timeout_On_Non_Existing_Database()
		{

			string clusterPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "notfound.cluster");
			File.WriteAllText(clusterPath, "local:thisClusterShouldNotExist@127.0.0.1:4566");
			var options = new FdbConnectionOptions { ClusterFile = clusterPath };

			using (var db = await Fdb.OpenAsync(options, this.Cancellation))
			{
				bool exists = false;
				var err = FdbError.Success;
				try
				{
					using(var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
					{
						tr.Timeout = 250; // ms
						Log("check ...");
						await tr.GetAsync(Slice.FromString("key_not_found"));
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

		[Test]
		public void Test_Can_Serialize_Connection_Options()
		{
			var options = new FdbConnectionOptions();
			Assert.That(options.ToString(), Is.EqualTo("cluster_file=default"));

			options = new FdbConnectionOptions
			{
				ClusterFile = "X:\\some\\path\\to\\fdb.cluster",
				Root = FdbDirectoryPath.Combine("Hello", "World"),
			};
			Assert.That(options.ToString(), Is.EqualTo(@"cluster_file=X:\some\path\to\fdb.cluster; root=/Hello/World"));

			options = new FdbConnectionOptions
			{
				ClusterFile = "X:\\some\\path\\to\\fdb.cluster",
				ReadOnly = true,
				DefaultTimeout = TimeSpan.FromSeconds(42.5),
				DefaultRetryLimit = 123,
				DataCenterId = "AC/DC",
				MachineId = "Marble Machine X"
			};
			Assert.That(options.ToString(), Is.EqualTo(@"cluster_file=X:\some\path\to\fdb.cluster; readonly; timeout=42.5; retry_limit=123; dc_id=AC/DC; machine_id=""Marble Machine X"""));

			options = new FdbConnectionOptions
			{
				ClusterFile = "/etc/foundationdb/fdb.cluster",
				MachineId = "James \"The Machine\" Wade",
				DefaultTimeout = TimeSpan.FromTicks((long) (Math.PI * TimeSpan.TicksPerSecond)),
			};
			Assert.That(options.ToString(), Is.EqualTo(@"cluster_file=/etc/foundationdb/fdb.cluster; timeout=3.1415926; machine_id=""James \""The Machine\"" Wade"""));
		}

	}
}
