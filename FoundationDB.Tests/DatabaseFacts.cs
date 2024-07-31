﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable ReplaceAsyncWithTaskReturn
// ReSharper disable VariableLengthStringHexEscapeSequence

namespace FoundationDB.Client.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Net;
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
				Assert.That(db.Root.Path, Is.EqualTo(FdbPath.Root));
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
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = @".\file_not_found.cluster", Root = FdbPath.Root["Tests"] }, this.Cancellation), FdbError.NoClusterFileFound, "Should fail if cluster file is missing");

			// unreachable path
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = @"C:\..\..\fdb.cluster", Root = FdbPath.Root["Tests"] }, this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");

			// malformed path
			await TestHelpers.AssertThrowsFdbErrorAsync(() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = @"FOO:\invalid$path!/fdb.cluster", Root = FdbPath.Root["Tests"] }, this.Cancellation), FdbError.NoClusterFileFound, "Should fail if path is malformed");
		}

		[Test]
		public async Task Test_Open_Or_CreateCluster_With_Corrupted_ClusterFile_Should_Fail()
		{
			// Using a corrupted cluster file should fail with "ConnectionStringInvalid"

			// write some random bytes into a cluster file
			var rnd = new Random();
			var bytes = new byte[128];
			rnd.NextBytes(bytes);
			string path = System.IO.Path.GetTempFileName();
			try
			{
				await System.IO.File.WriteAllBytesAsync(path, bytes, this.Cancellation);

				//note: we have to perform at least one read operation, before the client actually attempts to connect to the cluster,
				// so we have to open with a custom Root path (the directory layer will have to read from the cluster to find the prefix!)

				await TestHelpers.AssertThrowsFdbErrorAsync(
					() => Fdb.OpenAsync(new FdbConnectionOptions { ClusterFile = path, Root = FdbPath.Root["Tests"] }, this.Cancellation),
					FdbError.ConnectionStringInvalid,
					"Should fail if file is corrupted"
				);
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
				Assert.That(db.Root.Path, Is.EqualTo(FdbPath.Root), ".Root");
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
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					tr.Options.WithReadAccessToSystemKeys();
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

				Assert.That(status!.Client, Is.Not.Null);
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
			using (var db = await OpenTestDatabaseAsync())
			{
				//TODO: how can we test that it is successful ?

				db.Options.WithLocationCacheSize(1000);
				db.Options.WithLocationCacheSize(0); // does this disable location cache ?
				db.Options.WithLocationCacheSize(9001);

				// should reject negative numbers
				Assert.That(() => db.Options.WithLocationCacheSize(-123), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.InvalidOptionValue).And.Property("Success").False);
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
				Assert.That(db.Root.Path, Is.Not.EqualTo(FdbPath.Root));

				var dl = db.DirectoryLayer;
				Assert.That(dl, Is.Not.Null);
				Assert.That(dl.Content, Is.Not.Null);
				Assert.That(dl.Content, Is.EqualTo(SubspaceLocation.Root), "Root DL should be located at the top");

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var root = await db.Root.Resolve(tr);
					Assert.That(root, Is.Not.Null);
					Assert.That(root!.Path, Is.EqualTo(db.Root.Path));
					Assert.That(root.DirectoryLayer, Is.SameAs(dl));
				}
			}
		}

		[Test]
		public async Task Test_Check_Timeout_On_Non_Existing_Database()
		{
			string clusterPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "notfound.cluster");
			await File.WriteAllTextAsync(clusterPath, "local:thisClusterShouldNotExist@127.0.0.1:4566", this.Cancellation);
			var options = new FdbConnectionOptions { ClusterFile = clusterPath };

			using (var db = await Fdb.OpenAsync(options, this.Cancellation))
			{
				bool exists = false;
				var err = FdbError.Success;
				try
				{
					using(var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						tr.Options.Timeout = 250; // ms
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
				Root = FdbPath.Parse("/Hello/World"),
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

		[Test, Category("LongRunning")]
		public async Task Test_Can_Get_Main_Thread_Busyness()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var value = db.GetMainThreadBusyness();
				Log($"Current busyness: {value:N4} ({value * 100:N2}%)");
				Assert.That(value, Is.GreaterThanOrEqualTo(0).And.LessThan(1), "Value must be [0, 1]");

				var start = DateTime.Now;

				// we will call this multiple times, when idle
				Log("Querying busyness when idle...");
				for (int i = 0; i < 3; i++)
				{
					await Task.Delay(1000, this.Cancellation);
					value = db.GetMainThreadBusyness();
					Assert.That(value, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(1), "Value must be [0, 1]");
					Log($"> T+{(DateTime.Now - start).TotalSeconds:N2}s: {value:N4} ({value * 100,6:N2}%) {new string('#', (int) Math.Ceiling(value * 100))}");
				}

				// read a bunch of keys to generate _some_ activity
				Log("Querying busyness with some load in the background...");
				var t = Task.WhenAll(Enumerable.Range(0, 200).Select(i => db.ReadAsync(async tr =>
				{
					await tr.GetValuesAsync(Enumerable.Range(0, 200).Select(x => TuPack.EncodeKey("Hello", i, x)));
				}, this.Cancellation)).ToArray());

				// we will call this multiple times, when idle
				for (int i = 0; i < 3; i++)
				{
					if (i > 0) await Task.Delay(1000, this.Cancellation);
					value = db.GetMainThreadBusyness();
					Assert.That(value, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(1), "Value must be [0, 1]");
					Log($"> T+{(DateTime.Now - start).TotalSeconds:N2}s: {value:N4} ({value * 100,6:N2}%) {new string('#', (int) Math.Ceiling(value * 100))}");
				}

				await t;
			}
		}

		[Test]
		public async Task Test_Can_Create_Snapshot()
		{
			using (var db = await OpenTestDatabaseAsync())
			{

				// the UID must be valid. If not we should get error code FdbError.SnapshotInvalidUidString (2509)
				Log("Creating snapshot with invalid uid...");
				var ex = Assert.ThrowsAsync<FdbException>(async () => await db.CreateSnapshotAsync("invalid_uid", "test", this.Cancellation), "Should fail because the snapshot uid is invalid")!;
				Log($"> {ex.Code}: {ex.Message}");
				Assert.That(ex.Code, Is.EqualTo(FdbError.SnapshotInvalidUidString));

				// the operation _may_ fail with FdbError.SnapshotPathNotWhitelisted (2505) if the server is not pre-configured correctly
				var uid = Guid.NewGuid().ToString("n");
				try
				{
					Log($"Creating snapshot with valid uid '{uid}'...");
					await Await(db.CreateSnapshotAsync(uid, "test", this.Cancellation), TimeSpan.FromSeconds(30));
					Log("> Done");
				}
				catch (FdbException e)
				{
					Log($"> {e.Code}: {e.Message}");
					if (e.Code == FdbError.SnapshotPathNotWhitelisted)
					{ // the test is inconclusive because the server is not configured properly for snapshots
						Assert.Inconclusive("Path to snapshot create binary is not approved by the server.");
					}
					throw;
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Server_Protocol()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				Log("Getting server protocol version...");
				var ver = await Await(db.GetServerProtocolAsync(this.Cancellation), TimeSpan.FromSeconds(10));
				Log($"> 0x{ver:x} ({ver})");

				// The upper 32 bits should be 0x0fdb00b0
				Assert.That(ver >> 32, Is.EqualTo(0xfdb00b0), "Invalid FDB protocol version");

				// The lower 32 bits should be the cluster version.
				// note: this is tricky to test because we don't know which server version will be used when running the test, we will _assume_ that it is at least 7.x something

				// the format is "XYZR0000" for version "vX.Y.Z" and R is the "dev" version which may or may not be used anymore (unclear)
				// for example: "73000000" is 7.3, and "61020000" is 6.1 dev version 2

				// the upper 8 bits are the 
				var majorMinor = (ver >> 24) & 0xFF;
				if (majorMinor <= 0x73)
				{ // known versions
					Assert.That(majorMinor, Is.AnyOf(0x61, 0x62, 0x63, 0x70, 0x71, 0x72, 0x73));
				}
				else
				{ // future version?
					Assert.Inconclusive($"Unknown protocol version: {majorMinor:x} for cluster protocol 0x{ver:x}");
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Client_Status()
		{
			using (var db = await OpenTestDatabaseAsync())
			{

				// we need to read something so that the client connects to at least one storage server!
				await db.ReadAsync((tr) => tr.GetAsync(Slice.FromString("HelloWorld")), this.Cancellation);

				Log("Getting client status...");
				var status = await Fdb.System.GetClientStatusAsync(db, this.Cancellation);
				Assert.That(status, Is.Not.Null);
				Assert.That(status.Exists(), Is.True);
				Assert.That(status.RawData.Count, Is.GreaterThan(0));
				Assert.That(status.JsonData, Is.Not.Null);

				Log($"Received status: {status.RawData.Count:N0} bytes");
#if FULL_DEBUG
				Log(status.JsonData?.ToJsonIndented());
#else
				Log(status.JsonData?.ToJson());
#endif

				Assert.That(status.Healthy, Is.True);
				Assert.That(status.InitializationError, Is.Null);
				Assert.That(status.ClusterId, Is.Not.Null.Or.Empty);
				Assert.That(status.CurrentCoordinator, Is.Not.Null);
				Assert.That(status.CurrentCoordinator.IsValid(), Is.True);
				Assert.That(status.Coordinators, Is.Not.Null.Or.Empty.And.All.Not.Null);
				Assert.That(status.Coordinators, Does.Contain(status.CurrentCoordinator));
				Assert.That(status.Connections, Is.Not.Null.Or.Empty.And.All.Not.Null);
				Assert.That(status.CommitProxies, Is.Not.Null.Or.Empty.And.All.Not.Null);
				Assert.That(status.GrvProxies, Is.Not.Null.Or.Empty.And.All.Not.Null);
				Assert.That(status.StorageServers, Is.Not.Null.Or.Empty.And.All.Not.Null);
				Assert.That(status.NumConnectionsFailed, Is.Not.Null.And.GreaterThanOrEqualTo(0));

				Log($"Coordinators: current is {status.CurrentCoordinator}");
				foreach(var coord in status.Coordinators)
				{
					Log($"- {coord}");
					Assert.That(coord, Is.Not.Null);
					Assert.That(coord.Address, Is.Not.Null.And.Not.EqualTo(IPAddress.None));
					Assert.That(coord.Port, Is.GreaterThan(0));
				}
				Log($"Connections: {status.Connections.Length}");
				foreach (var conn in status.Connections)
				{
					Log($"- {conn.Address}: {conn.Status}, {conn.ProtocolVersion}");
					Assert.That(conn.Address, Is.Not.Null);
					Assert.That(conn.Address.IsValid());
					Assert.That(conn.Address.Address, Is.Not.Null.Or.EqualTo(IPAddress.None));
					Assert.That(conn.Address.Port, Is.GreaterThan(0));
					Assert.That(conn.Status, Is.AnyOf("connected", "connecting", "disconnected", "failed"));
					Assert.That(conn.Compatible, Is.Not.Null);
					Assert.That(conn.BytesReceived, Is.Not.Null.And.GreaterThanOrEqualTo(0));
					Assert.That(conn.BytesSent, Is.Not.Null.And.GreaterThanOrEqualTo(0));
					Assert.That(conn.ProtocolVersion, Is.Null.Or.Not.Empty); // sometimes missing (maybe the client did not connect to this coordinator yet?)
				}

				Log($"Storage Servers: {status.StorageServers.Length}");
				foreach (var stor in status.StorageServers)
				{
					Log($"- {stor.Address}: {stor.SSID}");
					Assert.That(stor.Address, Is.Not.Null);
					Assert.That(stor.Address.IsValid());
					Assert.That(stor.Address.Address, Is.Not.Null.And.Not.EqualTo(IPAddress.None));
					Assert.That(stor.Address.Port, Is.GreaterThan(0));
					Assert.That(stor.SSID, Is.Not.Null.Or.Empty);
				}
			}
		}

	}

}
