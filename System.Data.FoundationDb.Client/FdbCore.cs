using System;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.FoundationDb.Client
{

	public enum FdbError
	{
		Success = 0,
	}

	public enum FdbNetworkOption
	{
		None = 0,

		/// <summary>IP:PORT
		/// (DEPRECATED)</summary>
		LocalAddress = 10,
		/// <summary>Path to cluster file
		/// (DEPRECATED)</summary>
		ClusterFile = 20,
		/// <summary>Path to output directory (or NULL for current working directory).
		/// Enable traces output to a file in a directory of the clients choosing.</summary>
		TraceEnable = 30,
	}

	public enum FdbClusterOption
	{
		None = 0,
		/// <summary>This option is only a placeholder for C compatibility and should not be used</summary>
		Invalid = -1,
	}

	public static partial class FdbCore
	{

		public static string NativeLibPath = ".";

		public static int GetMaxApiVersion()
		{
			return FdbNativeStub.GetMaxApiVersion();
		}

		public static bool Success(FdbError code)
		{
			return code == FdbError.Success;
		}

		public static bool Failed(FdbError code)
		{
			return code != FdbError.Success;
		}

		internal static void DieOnError(FdbError code)
		{
			if (Failed(code)) throw MapToException(code);
		}

		public static string GetErrorMessage(FdbError code)
		{
			return FdbNativeStub.GetError(code);
		}

		public static Exception MapToException(FdbError code)
		{
			if (code == FdbError.Success) return null;

			string msg = GetErrorMessage(code);
			if (msg == null) throw new InvalidOperationException(String.Format("Unexpected error code {0}", (int)code));

			switch(code)
			{
				//TODO!
				default: 
					throw new InvalidOperationException(msg);
			}
		}

		#region Network Event Loop...

		private static Thread s_eventLoop;
		private static bool s_eventLoopStarted;

		private static void StartEventLoop()
		{
			if (s_eventLoop == null)
			{
				var thread = new Thread(new ThreadStart(EventLoop));
				thread.Name = "FoundationDB Event Loop";
				thread.IsBackground = true;
				s_eventLoop = thread;
				try
				{
					thread.Start();
					s_eventLoopStarted = true;
				}
				catch (Exception)
				{
					s_eventLoopStarted = false;
					throw;
				}
			}
		}

		private static void StopEventLoop()
		{
			if (s_eventLoopStarted)
			{
				var err = FdbNativeStub.StopNetwork();
				s_eventLoopStarted = false;

				var thread = s_eventLoop;
				if (thread != null && thread.ThreadState == ThreadState.Running)
				{
					thread.Abort();
					thread.Join(TimeSpan.FromSeconds(1));
					s_eventLoop = null;
				}
			}
		}

		private static void EventLoop()
		{
			try
			{
				while (true)
				{
					var err = FdbNativeStub.RunNetwork();
					if (err == FdbError.Success)
					{ // Stop received
						return;
					}
					Console.WriteLine("RunNetwork returned " + err + " : " + GetErrorMessage(err));
				}
			}
			catch (Exception e)
			{
				if (e is ThreadAbortException)
				{ // bie bie
					Thread.ResetAbort();
					return;
				}
			}
		}

		#endregion

		public static Task<FdbCluster> CreateClusterAsync(string path)
		{
			var future = FdbNativeStub.CreateCluster(path);

			Console.WriteLine("CreateCluster => 0x" + future.Handle.ToString("x"));

			return FdbFuture<FdbCluster>
				.FromHandle(future,
				(h) =>
				{
					ClusterHandle cluster;
					var err = FdbNativeStub.FutureGetCluster(h, out cluster);
					if (err != FdbError.Success)
					{
						cluster.Dispose();
						throw MapToException(err);
					}
					Console.WriteLine("FutureGetCluster => 0x" + cluster.Handle.ToString("x"));
					return new FdbCluster(cluster);
				})
				.Task;
		}

		internal static Task<FdbDatabase> CreateDatabaseAsync(FdbCluster cluster, string name)
		{
			if (cluster == null) throw new ArgumentNullException("cluster");
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

			if (cluster.Handle.IsInvalid) throw new InvalidOperationException("Cannot connect to invalid cluster");

			var future = FdbNativeStub.CreateClusterDatabase(cluster.Handle, name);

			Console.WriteLine("CreateClusterDatabase => 0x" + future.Handle.ToString("x"));

			return FdbFuture<FdbDatabase>
				.FromHandle(future,
				(h) =>
				{
					DatabaseHandle database;
					var err = FdbNativeStub.FutureGetDatabase(h, out database);
					if (err != FdbError.Success)
					{
						database.Dispose();
						throw MapToException(err);
					}
					Console.WriteLine("FutureGetDatabase => 0x" + database.Handle.ToString("x"));

					return new FdbDatabase(cluster, database, name);
				})
				.Task;
		}

		internal static FdbTransaction CreateTransaction(FdbDatabase database)
		{
			if (database == null) throw new ArgumentNullException("database");

			if (database.Handle.IsInvalid) throw new InvalidOperationException("Cannot create a transaction on an invalid database");

			TransactionHandle handle;
			var err = FdbNativeStub.DatabaseCreateTransaction(database.Handle, out handle);
			Console.WriteLine("DatabaseCreateTransaction => " + err.ToString() + ", 0x" + handle.Handle.ToString("x"));
			if (Failed(err))
			{
				handle.Dispose();
				throw MapToException(err);
			}

			return new FdbTransaction(database, handle);
		}

		public static void Start()
		{
			DieOnError(FdbNativeStub.SelectApiVersion(FdbNativeStub.FDB_API_VERSION));

			Console.WriteLine("Setting up network...");
			DieOnError(FdbNativeStub.SetupNetwork());
			Console.WriteLine("Got Network");

			Console.WriteLine("Starting event loop");
			StartEventLoop();

		}

		public static void Stop()
		{
			Console.WriteLine("Stopping event loop");
			StopEventLoop();
			Console.WriteLine("Stopped");
		}

		public static Task<FdbCluster> ConnectAsync(string clusterPath)
		{
			Console.WriteLine("Connecting to cluster... " + clusterPath);
			return CreateClusterAsync(clusterPath);
		}

	}

}
