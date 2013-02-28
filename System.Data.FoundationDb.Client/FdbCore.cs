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

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.FoundationDb.Client
{

	public static partial class FdbCore
	{

		public static string NativeLibPath = ".";
		public static string TracePath = @"c:\temp\fdb";

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
			if (true || msg == null) throw new InvalidOperationException(String.Format("Unexpected error code {0}", (int)code));

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
				Debug.WriteLine("Starting event loop...");

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
				if (thread != null && thread.ThreadState == System.Threading.ThreadState.Running)
				{
					thread.Abort();
					thread.Join(TimeSpan.FromSeconds(1));
					s_eventLoop = null;
				}
			}
		}

		private static void EventLoop()
		{
			Debug.WriteLine("Event Loop running..");
			try
			{
				while (true)
				{
					var err = FdbNativeStub.RunNetwork();
					if (err == FdbError.Success)
					{ // Stop received
						return;
					}
					Debug.WriteLine("RunNetwork returned " + err + " : " + GetErrorMessage(err));
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
			finally
			{
				Debug.WriteLine("Event Loop stopped");
			}
		}

		#endregion

		public static Task<FdbCluster> CreateClusterAsync(string path)
		{
			var future = FdbNativeStub.CreateCluster(path);

			Debug.WriteLine("CreateCluster => 0x" + future.Handle.ToString("x"));

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
					Debug.WriteLine("FutureGetCluster => 0x" + cluster.Handle.ToString("x"));
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

			Debug.WriteLine("CreateClusterDatabase => 0x" + future.Handle.ToString("x"));

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
					Debug.WriteLine("FutureGetDatabase => 0x" + database.Handle.ToString("x"));

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
			Debug.WriteLine("DatabaseCreateTransaction => " + err.ToString() + ", 0x" + handle.Handle.ToString("x"));
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

			Debug.WriteLine("Setting up network...");

			if (TracePath != null)
			{
				unsafe
				{
					byte[] data = FdbNativeStub.ToNativeString(TracePath);
					fixed (byte* ptr = data)
					{
						DieOnError(FdbNativeStub.NetworkSetOption(FdbNetworkOption.TraceEnable, ptr, data.Length));
					}
				}
			}

			DieOnError(FdbNativeStub.SetupNetwork());
			Debug.WriteLine("Got Network");

			StartEventLoop();

		}

		public static void Stop()
		{
			Debug.WriteLine("Stopping event loop");
			StopEventLoop();
			Debug.WriteLine("Stopped");
		}

		public static Task<FdbCluster> ConnectAsync(string clusterPath)
		{
			Debug.WriteLine("Connecting to cluster... " + clusterPath);
			return CreateClusterAsync(clusterPath);
		}

	}

}
