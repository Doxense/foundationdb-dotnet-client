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

#undef DEBUG_THREADS

namespace FoundationDB.Client
{
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class Fdb
	{

		/// <summary>Flag indicating if FDB has been initialized or not</summary>
		private static bool s_started;

		private static EventHandler s_appDomainUnloadHandler;

		internal static readonly byte[] EmptyArray = new byte[0];

		/// <summary>Keys cannot exceed 10,000 bytes</summary>
		internal const int MaxKeySize = 10 * 1000;

		/// <summary>Values cannot exceed 100,000 bytes</summary>
		internal const int MaxValueSize = 100 * 1000;

		/// <summary>Maximum size of total written keys and values by a transaction</summary>
		internal const int MaxTransactionWriteSize = 10 * 1024 * 1024;

		public static int GetMaxApiVersion()
		{
			return FdbNative.GetMaxApiVersion();
		}

		/// <summary>Returns true if the error code represents a success</summary>
		public static bool Success(FdbError code)
		{
			return code == FdbError.Success;
		}

		/// <summary>Returns true if the error code represents a failure</summary>
		public static bool Failed(FdbError code)
		{
			return code != FdbError.Success;
		}

		/// <summary>Throws an exception if the code represents a failure</summary>
		internal static void DieOnError(FdbError code)
		{
			if (Failed(code)) throw MapToException(code);
		}

		/// <summary>Return the error message matching the specified error code</summary>
		public static string GetErrorMessage(FdbError code)
		{
			return FdbNative.GetError(code);
		}

		/// <summary>Maps an error code into an Exception (to be throwned)</summary>
		/// <param name="code"></param>
		/// <returns>Exception object corresponding to the error code, or null if the code is not an error</returns>
		public static Exception MapToException(FdbError code)
		{
			if (code == FdbError.Success) return null;

			string msg = GetErrorMessage(code);
			if (msg == null) throw new FdbException(code, String.Format("Unexpected error code {0}", (int)code));

			//TODO: create a custom FdbException to be able to store the error code and error message
			switch(code)
			{
				case FdbError.TimedOut: return new TimeoutException("Operation timed out");
				case FdbError.LargeAllocFailed: return new OutOfMemoryException("Large block allocation failed");
				//TODO!
				default: 
					return new FdbException(code, msg);
			}
		}

		#region Network Thread / Event Loop...

		private static Thread s_eventLoop;
		private static bool s_eventLoopStarted;
		private static bool s_eventLoopRunning;
		private static int? s_eventLoopThreadId;

		/// <summary>Starts the thread running the FDB event loop</summary>
		private static void StartEventLoop()
		{
			if (s_eventLoop == null)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "StartEventLoop", "Starting network thread...");

				var thread = new Thread(new ThreadStart(EventLoop));
				thread.Name = "FdbNetworkLoop";
				thread.IsBackground = true;
				thread.Priority = ThreadPriority.AboveNormal;
				s_eventLoop = thread;
				try
				{
					thread.Start();
					s_eventLoopStarted = true;
				}
				catch (Exception e)
				{
					s_eventLoopStarted = false;
					s_eventLoop = null;
					if (Logging.On) Logging.Exception(typeof(Fdb), "StartEventLoop", e);
					throw;
				}
			}
		}

		/// <summary>Stops the thread running the FDB event loop</summary>
		private static void StopEventLoop()
		{
			if (s_eventLoopStarted)
			{

				// We cannot be called from the network thread itself, or else we will dead lock !
				Fdb.EnsureNotOnNetworkThread();

				if (Logging.On) Logging.Verbose(typeof(Fdb), "StopEventLoop", "Stopping network thread...");

				var err = FdbNative.StopNetwork();
				s_eventLoopStarted = false;


				var thread = s_eventLoop;
				if (thread != null && thread.IsAlive)
				{
					// BUGBUG: specs says that we need to wait for the network thread to stop gracefuly, or else data integrity may not be guaranteed...
					// We should wait for a bit, and only attempt to Abort() the thread after a timeout (30sec ? more ?)

					// keep track of how much time it took to stop...
					var duration = Stopwatch.StartNew();

					try
					{
						//TODO: replace with a ManualResetEvent that would get signaled at the end of the event loop ?
						while (thread.IsAlive && duration.Elapsed.TotalSeconds < 5)
						{
							// wait a bit...
							Thread.Sleep(250);
						}

						if (thread.IsAlive)
						{
							if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", String.Format("The fdb network thread has not stopped after {0} seconds. Forcing shutdown...", duration.Elapsed.TotalSeconds.ToString("N0")));

							// Force a shutdown
							thread.Abort();

							bool stopped = thread.Join(TimeSpan.FromSeconds(30));
							//REVIEW: is this even usefull? If the thread is stuck in a native P/Invoke call, it won't get notified until it returns to managed code ...
							// => in that case, we have a zombie thread on our hands...

							if (!stopped)
							{
								if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", String.Format("The fdb network thread failed to stop after more than {0} seconds. Transaction integrity may not be guaranteed.", duration.Elapsed.TotalSeconds.ToString("N0")));
							}
						}
					}
					catch (ThreadAbortException)
					{
						// Should not happen, unless we are called from a thread that is itself being stopped ?
					}
					finally
					{
						s_eventLoop = null;
						duration.Stop();
						if (duration.Elapsed.TotalSeconds >= 20)
						{
							if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", String.Format("The fdb network thread took a long time to stop ({0} seconds).", duration.Elapsed.TotalSeconds.ToString("N0")));
						}
					}
				}
			}
		}

		/// <summary>Entry point for the Network Thread</summary>
		private static void EventLoop()
		{
			try
			{
				s_eventLoopRunning = true;

				s_eventLoopThreadId = Thread.CurrentThread.ManagedThreadId;

				if (Logging.On) Logging.Verbose(typeof(Fdb), "EventLoop", String.Format("FDB Event Loop running on thread #{0}...", s_eventLoopThreadId.Value));

				var err = FdbNative.RunNetwork();
				if (err != FdbError.Success)
				{ // Stop received
					//TODO: logging ?
					if (Logging.On) Logging.Error(typeof(Fdb), "EventLoop", String.Format("The fdb network thread returned with error code {0}: {1}", err, GetErrorMessage(err)));
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
				if (Logging.On) Logging.Verbose(typeof(Fdb), "EventLoop", "FDB Event Loop stopped");

				s_eventLoopThreadId = null;
				s_eventLoopRunning = false;
			}
		}

		/// <summary>Returns true if the Network thread start is executing, otherwise falsse</summary>
		public static bool IsNetworkRunning
		{
			get { return s_eventLoopRunning; }
		}

		/// <summary>Returns 'true' if we are currently running on the Event Loop thread</summary>
		internal static bool IsNetworkThread
		{
			get
			{
				var eventLoopThreadId = s_eventLoopThreadId;
				return eventLoopThreadId.HasValue && Thread.CurrentThread.ManagedThreadId == eventLoopThreadId.Value;
			}
		}

		/// <summary>Throws if the current thread is the Network Thread.</summary>
		/// <remarks>Should be used to ensure that we do not execute tasks continuations from the network thread, to avoid dead-locks.</remarks>
		internal static void EnsureNotOnNetworkThread([CallerMemberName]string callerMethod = null)
		{
#if DEBUG_THREADS
			if (Logging.On && Logging.IsVerbose)
			{
				Logging.Verbose(null, callerMethod, String.Format("[Executing on thread #{0}]", Thread.CurrentThread.ManagedThreadId));
			}
#endif

			if (Fdb.IsNetworkThread)
			{ // cannot commit from same thread as the network loop because it could lead to a deadlock
				FailCannotExecuteOnNetworkThread();
			}
		}

		private static void FailCannotExecuteOnNetworkThread()
		{
#if DEBUG_THREADS
			if (Debugger.IsAttached) Debugger.Break();
#endif
			throw Fdb.Errors.CannotExecuteOnNetworkThread();
		}

		#endregion

		#region Cluster...

		/// <summary>Opens a connection to an existing FoundationDB cluster using the default cluster file</summary>
		/// <param name="ct">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbCluster, or an exception</returns>
		public static Task<FdbCluster> CreateClusterAsync(CancellationToken ct = default(CancellationToken))
		{
			return CreateClusterAsync(null, ct);
		}

		/// <summary>Opens a connection to an existing FDB Cluster</summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="ct">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbCluster, or an exception</returns>
		public static Task<FdbCluster> CreateClusterAsync(string clusterFile, CancellationToken ct = default(CancellationToken))
		{
			EnsureIsStarted();

			// "" should also be considered to mean "default cluster file"
			if (string.IsNullOrEmpty(clusterFile)) clusterFile = null;

			if (Logging.On) Logging.Info(typeof(Fdb), "CreateClusterAsync", clusterFile == null ? "Connecting to default cluster..." : String.Format("Connecting to cluster using '{0}' ...", clusterFile));

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			//TODO: check the path ? (exists, readable, ...)
			var future = FdbNative.CreateCluster(clusterFile);

			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					ClusterHandle cluster;
					var err = FdbNative.FutureGetCluster(h, out cluster);
					if (err != FdbError.Success)
					{
						cluster.Dispose();
						throw MapToException(err);
					}
					return new FdbCluster(cluster, clusterFile);
				},
				ct
			);
		}

		#endregion

		#region Database...

		/// <summary>
		/// Open the "DB" database on the cluster specified by the default cluster file
		/// </summary>
		/// <param name="ct">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		public static Task<FdbDatabase> OpenAsync(CancellationToken ct = default(CancellationToken))
		{
			return OpenAsync(clusterFile: null, dbName: null, globalSpace: FdbSubspace.Empty, ct: ct);
		}

		/// <summary>
		/// Open the "DB" database on the cluster specified by the default cluster file, and with the specified global space
		/// </summary>
		/// <param name="globalSpace">Global subspace used as a prefix for all keys and layers</param>
		/// <param name="ct">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		public static Task<FdbDatabase> OpenAsync(FdbSubspace globalSpace, CancellationToken ct = default(CancellationToken))
		{
			return OpenAsync(clusterFile: null, dbName: null, globalSpace: globalSpace, ct: ct);
		}

		/// <summary>
		/// Open a database on the specified cluster
		/// </summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="dbName">Name of the database, or "DB" if not specified.</param>
		/// <param name="ct">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of 1.0, the only supported database name is "DB"</remarks>
		/// <exception cref="System.InvalidOperationException">If <paramref name="dbName"/> is anything other than "DB"</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		public static Task<FdbDatabase> OpenAsync(string clusterFile, string dbName, CancellationToken ct = default(CancellationToken))
		{
			return OpenAsync(clusterFile, dbName, FdbSubspace.Empty, ct);
		}

		/// <summary>
		/// Open a database on the specified cluster
		/// </summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="dbName">Name of the database. Must be 'DB'</param>
		/// <param name="globalSpace">Global subspace used as a prefix for all keys and layers</param>
		/// <param name="ct">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of 1.0, the only supported database name is 'DB'</remarks>
		/// <exception cref="System.InvalidOperationException">If <paramref name="dbName"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		public static async Task<FdbDatabase> OpenAsync(string clusterFile, string dbName, FdbSubspace globalSpace, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			dbName = dbName ?? "DB";
			globalSpace = globalSpace ?? FdbSubspace.Empty;

			if (Logging.On) Logging.Info(typeof(Fdb), "OpenAsync", String.Format("Connecting to database '{0}' using cluster file '{1}' and subspace '{2}' ...", dbName, clusterFile, globalSpace.ToString()));

			FdbCluster cluster = null;
			FdbDatabase db = null;
			bool success = false;
			try
			{
				cluster = await CreateClusterAsync(clusterFile, ct).ConfigureAwait(false);
				//note: since the cluster is not provided by the caller, link it with the database's Dispose()
				db = await cluster.OpenDatabaseAsync(dbName, globalSpace, true, ct).ConfigureAwait(false);
				success = true;
				return db;
			}
			finally
			{
				if (!success)
				{
					// cleanup the cluter if something went wrong
					if (db != null) db.Dispose();
					if (cluster != null) cluster.Dispose();
				}
			}
		}

		#endregion

		/// <summary>Ensure that we have loaded the C API library, and that the Network Thread has been started</summary>
		private static void EnsureIsStarted()
		{
			if (!s_eventLoopStarted) Start();
		}

		/// <summary>Select the correct API version, and start the Network Thread</summary>
		public static void Start()
		{
			if (s_started) return;

			//BUGBUG: Specs say we cannot restart the network thread anymore in the process after stoping it ! :(

			s_started = true;

			// register with the AppDomain to ensure that everyting is cleared when the process exists
			s_appDomainUnloadHandler = (sender, args) =>
			{
				if (s_started)
				{
					//note: since app domain is unloading, the logger may also have already stopped...
					if (Logging.On) Logging.Verbose(typeof(Fdb), "AppDomainUnloadHandler", "AppDomain is unloading, stopping FoundationDB Network Thread...");
					Stop();
				}
			};
			AppDomain.CurrentDomain.DomainUnload += s_appDomainUnloadHandler;
			AppDomain.CurrentDomain.ProcessExit += s_appDomainUnloadHandler;

			if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Selecting fdb API version {0}", FdbNative.FDB_API_VERSION));

			DieOnError(FdbNative.SelectApiVersion(FdbNative.FDB_API_VERSION));

			if (!string.IsNullOrWhiteSpace(Fdb.Options.TracePath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will trace client activity in '{0}'", Fdb.Options.TracePath));
				// create trace directory if missing...
				if (!Directory.Exists(Fdb.Options.TracePath)) Directory.CreateDirectory(Fdb.Options.TracePath);

				unsafe
				{
					var data = FdbNative.ToNativeString(Fdb.Options.TracePath, nullTerminated: false);
					fixed (byte* ptr = data.Array)
					{
						DieOnError(FdbNative.NetworkSetOption(FdbNetworkOption.TraceEnable, ptr + data.Offset, data.Count));
					}
				}
			}

			if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", "Setting up Network Thread...");

			DieOnError(FdbNative.SetupNetwork());

			if (Logging.On) Logging.Info(typeof(Fdb), "Start", "Network thread has been set up");

			StartEventLoop();
		}

		/// <summary>Stop the Network Thread</summary>
		public static void Stop()
		{
			if (s_started)
			{
				s_started = false;

				// unregister the event on the AppDomain
				AppDomain.CurrentDomain.DomainUnload -= s_appDomainUnloadHandler;
				s_appDomainUnloadHandler = null;

				if (Logging.On) Logging.Verbose(typeof(Fdb), "Stop", "Stopping Network Thread...");
				StopEventLoop();
				if (Logging.On) Logging.Info(typeof(Fdb), "Stop", "Network Thread stopped");
			}
		}

	}

}
