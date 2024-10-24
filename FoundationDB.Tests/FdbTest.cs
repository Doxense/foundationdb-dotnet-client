#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client.Tests
{
	using System.Runtime.CompilerServices;
	using DotNet.Testcontainers.Builders;
	using DotNet.Testcontainers.Configurations;
	using DotNet.Testcontainers.Containers;
	using Doxense.Serialization;
	using SnowBank.Testing;

	public class FdbServerTestContainer : IAsyncDisposable
	{

		public static FdbServerTestContainer? Global;

		public IContainer Container { get; }

		private TaskCompletionSource? ReadyCts { get; set; }

		public Task ReadyTask { get; private set; } = Task.FromException(new InvalidOperationException("Test container not started"));

		public string Tag { get; }

		public string Image { get; }

		public string VolumeName { get; }

		public string Description { get; }

		public string Id { get; }

		public int Port { get; }

		public string ConnectionString { get; }

		public FdbServerTestContainer(string name, string? tag = "7.3.47", int port = 4540, string volumeName = "fdb_test")
		{
			this.Description = "docker";
			this.Id = "docker";
			this.Port = port;
			this.Tag = tag!;
			this.Image = "foundationdb/foundationdb:" + tag;
			this.VolumeName = volumeName;

			this.ConnectionString = $"{this.Id}:{this.Description}@127.0.0.1:{this.Port}";

			// Create a new instance of a container.
			var container = new ContainerBuilder()
				.WithImage(this.Image)
				.WithName(name)
				.WithReuse(reuse: true)
				.WithPortBinding(port, port)
				.WithVolumeMount(this.VolumeName, "/var/fdb/data", AccessMode.ReadWrite)
				.WithEnvironment("FDB_NETWORKING_MODE", "host")
				.WithEnvironment("FDB_PORT", port.ToString(CultureInfo.InvariantCulture))
				.WithEnvironment("FDB_COORDINATOR_PORT", port.ToString(CultureInfo.InvariantCulture))
				//.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(port))
				.Build();

			this.Container = container;
		}

		public async Task StartContainer()
		{
			var cts = new TaskCompletionSource();
			this.ReadyCts = cts;
			this.ReadyTask = cts.Task;

			// Start the container.
			try
			{
				SimpleTest.Log($"Starting FdbServer test container for {this.ConnectionString}...");
				await this.Container.StartAsync().ConfigureAwait(false);
				SimpleTest.Log($"FdbServer test container '{this.Container.Name}' ready");
				cts.TrySetResult();
			}
			catch (Exception e)
			{
				cts.TrySetException(e);
			}
		}

		public async ValueTask DisposeAsync()
		{
			this.ReadyCts?.TrySetCanceled();
			this.ReadyTask = Task.FromException(new ObjectDisposedException(this.GetType().Name));
			await this.Container.DisposeAsync();
		}

	}


	/// <summary>Base class for all FoundationDB tests that will interact with a live FoundationDB cluster</summary>
	[NonParallelizable]
	[Category("Fdb-Client-Live")]
	[FixtureLifeCycle(LifeCycle.SingleInstance)]
	public abstract class FdbTest : FdbSimpleTest
	{

		protected int OverrideApiVersion;

		protected virtual Task OnBeforeAllTests() => Task.CompletedTask;

		[OneTimeSetUp]
		protected void BeforeAllTests()
		{
			// we use the name of the .NET target framework

			var target = GetRuntimeFrameworkMoniker();

			//HACKHACK: we need a solution to allocate a dynamic port for the container _before_ starting the container itself,
			// since we need to inject env variables with the port. We cannot use the dynamic port allocation of the builder
			// itself, since we would know the port after the start when it's too late!
			// => for now, we only need to switch on different versions of dotnet, so we use the major version to build a port number

			int port = 4520 + Environment.Version.Major;
			// - net6.0 -> 4526
			// - net8.0 -> 4528
			// - net9.0 -> 4529

			var name = "fdb-test-" + target;
			var volumeName = "fdb-test-" + target;

			var tag = Environment.GetEnvironmentVariable("FDB_TEST_DOCKER_TAG");
			if (string.IsNullOrEmpty(tag)) tag = "7.3.47"; //TODO: make this a constant somewhere visible?

			this.Server = FdbServerTestContainer.Global;
			if (this.Server == null)
			{
				var container = new FdbServerTestContainer(name, tag, port, volumeName);

				_ = container.StartContainer();

				this.Server = container;
				FdbServerTestContainer.Global = container;
			}

			var probe = FdbClientNativeExtensions.ProbeNativeLibraryPaths();
			if (probe.Path == null)
			{
				Assert.Fail($"Could not located the native client library for platform '{probe.Rid}'. Looked in the following places: {string.Join(", ", probe.ProbedPaths)}");
				return;
			}
			Fdb.Options.NativeLibPath = probe.Path;

			// We must ensure that FDB is running before executing the tests
			// => By default, we always use 
			if (Fdb.ApiVersion == 0)
			{
				int version = OverrideApiVersion;
				if (version == 0) version = Fdb.GetDefaultApiVersion();
				if (version > Fdb.GetMaxApiVersion())
				{
					Assume.That(version, Is.LessThanOrEqualTo(Fdb.GetMaxApiVersion()), "Unit tests require that the native fdb client version be at least equal to the current binding version!");
				}
				Fdb.Start(version);
			}
			else if (OverrideApiVersion != 0 && OverrideApiVersion != Fdb.ApiVersion)
			{
				//note: cannot change API version on the fly! :(
				Assume.That(Fdb.ApiVersion, Is.EqualTo(OverrideApiVersion), "The API version selected is not what this test is expecting!");
			}

			// call the hook if defined on the derived test class
			OnBeforeAllTests().GetAwaiter().GetResult();
		}

		[OneTimeTearDown]
		protected void AfterAllTests()
		{
			// call the hook if defined on the derived test class
			OnAfterAllTests().GetAwaiter().GetResult();
		}

		protected virtual Task OnAfterAllTests() => Task.CompletedTask;

		private FdbServerTestContainer? Server { get; set; }

		protected FdbServerTestContainer GetLocalServer()
		{
			var server = this.Server;
			Assume.That(server, Is.Not.Null, "Local test server container was not started!?");
			return server!;
		}

		/// <summary>Returns the currently running framework moniker (<c>"net6.0"</c>, <c>"net8.0"</c>, <c>"net9.0"</c>, ...)</summary>
		protected static string GetRuntimeFrameworkMoniker()
		{
			string moniker = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
			return moniker;
		}

		private async Task<FdbServerTestContainer> WaitForTestServerToBecomeReady()
		{
			this.Cancellation.ThrowIfCancellationRequested();

			var server = FdbServerTestContainer.Global!;
			await server.ReadyTask.WaitAsync(this.Cancellation);

			return server;
		}

		/// <summary>Connect to the local test database</summary>
		//[DebuggerStepThrough]
		protected async Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false)
		{
			var server = await this.WaitForTestServerToBecomeReady();

			var options = new FdbConnectionOptions
			{
				ConnectionString = server.ConnectionString,
				Root = FdbPath.Root, // core tests cannot rely on the DirectoryLayer!
				DefaultTimeout = TimeSpan.FromSeconds(15),
				ReadOnly = readOnly,
			};

			return await Fdb.OpenAsync(options, this.Cancellation);
		}

		/// <summary>Connect to the local test database</summary>
		//[DebuggerStepThrough]
		protected async Task<IFdbDatabase> OpenTestPartitionAsync([CallerMemberName] string? caller = null)
		{
			var suffix = FdbPath.Relative(GetType().GetFriendlyName(), caller!);
			
			// We already use a dedicated fdbserver docker image for each .NET runtime version, so we are isolated
			// from other processes that would be spawn by test runners that execute the same test suites for 
			// multiple .NET framework concurrently (ex: ReSharper when using "In all target frameworks" option).
			// 
			// We only have to protect against concurrent executions of multiple test methods interfering with
			// each other by using the caller's name has a subdirectory name.

			var path = FdbPath.Root[FdbPathSegment.Partition("Tests")][suffix];

			await FdbServerTestContainer.Global!.ReadyTask;

			var options = new FdbConnectionOptions
			{
				ConnectionString = FdbServerTestContainer.Global.ConnectionString,
				Root = path,
				DefaultTimeout = TimeSpan.FromSeconds(15),
			};

			return await Fdb.OpenAsync(options, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task CleanLocation(IFdbDatabase db, ISubspaceLocation location)
		{
			Log($"# Using location {location.Path}");
			return TestHelpers.CleanLocation(db, location, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task CleanSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			return TestHelpers.CleanSubspace(db, subspace, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			return TestHelpers.DumpSubspace(db, subspace, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbDatabase db, ISubspaceLocation path)
		{
			return TestHelpers.DumpLocation(db, path, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbReadOnlyTransaction tr, IKeySubspace subspace)
		{
			return TestHelpers.DumpSubspace(tr, subspace);
		}

		[DebuggerStepThrough]
		protected async Task DumpSubspace(IFdbReadOnlyTransaction tr, ISubspaceLocation location)
		{
			var subspace = await location.Resolve(tr);
			if (subspace != null)
			{
				await TestHelpers.DumpSubspace(tr, subspace);
			}
			else
			{
				Log($"# Location {location} not found!");
			}
		}

		[DebuggerStepThrough]
		protected async Task DeleteSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.ClearRange(subspace);
				await tr.CommitAsync();
			}
		}

		[DebuggerStepThrough]
		protected Task DumpTree(IFdbDatabase db, FdbDirectorySubspaceLocation location)
		{
			return TestHelpers.DumpTree(db, location, this.Cancellation);
		}

		#region Read/Write Helpers...

		protected Task<T> DbRead<T>(IFdbRetryable db, Func<IFdbReadOnlyTransaction, Task<T>> handler)
		{
			return db.ReadAsync(handler, this.Cancellation);
		}

		protected Task<List<T>> DbQuery<T>(IFdbRetryable db, Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>> handler)
		{
			return db.QueryAsync(handler, this.Cancellation);
		}

		protected Task DbWrite(IFdbRetryable db, Action<IFdbTransaction> handler)
		{
			return db.WriteAsync(handler, this.Cancellation);
		}

		protected Task DbWrite(IFdbRetryable db, Func<IFdbTransaction, Task> handler)
		{
			return db.WriteAsync(handler, this.Cancellation);
		}

		protected Task DbVerify(IFdbRetryable db, Func<IFdbReadOnlyTransaction, Task> handler)
		{
			return db.ReadAsync(async (tr) => { await handler(tr); return true; }, this.Cancellation);
		}

		#endregion

	}

}
