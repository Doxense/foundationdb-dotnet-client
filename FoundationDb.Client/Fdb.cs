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

#undef DEBUG_THREADS

namespace FoundationDb.Client
{
	using FoundationDb.Client.Native;
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	public static class Fdb
	{

		public static class Options
		{

			/// <summary>Default path to the native C API library</summary>
			public static string NativeLibPath = ".";

			/// <summary>Default path to the network thread tracing file</summary>
			public static string TracePath = null;

			public static void SetNativeLibPath(string path)
			{
				Fdb.Options.NativeLibPath = path;
			}

			public static void SetTraceEnable(string outputDirectory)
			{
				Fdb.Options.TracePath = outputDirectory;
			}

		}

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

		#region Key/Value serialization

		/// <summary>Ensures that a serialized key is valid</summary>
		/// <exception cref="System.ArgumentException">If the key is either null, empty, or exceeds the maximum allowed size (Fdb.MaxKeySize)</exception>
		internal static void EnsureKeyIsValid(Slice key)
		{
			if (key.IsNullOrEmpty)
			{
				if (key.Array == null) 
					throw new ArgumentException("Key cannot be null", "key");
				else
					throw new ArgumentException("Key cannot be empty.", "key");
			}
			if (key.Count > Fdb.MaxKeySize)
			{
				throw new ArgumentException(String.Format("Key is too big ({0} > {1}).", key.Count, Fdb.MaxKeySize), "key");
			}
		}

		/// <summary>Ensures that a serialized value is valid</summary>
		/// <exception cref="System.ArgumentException">If the value is either null, empty, or exceeds the maximum allowed size (Fdb.MaxValueSize)</exception>
		internal static void EnsureValueIsValid(Slice value)
		{
			if (!value.HasValue)
			{
				throw new ArgumentException("Value cannot be null", "value");
			}
			if (value.Count > Fdb.MaxValueSize)
			{
				throw new ArgumentException(String.Format("Value is too big ({0} > {1}).", value.Count, Fdb.MaxValueSize), "value");
			}
		}

		#endregion

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
#if DEBUG_THREADS
				Debug.WriteLine("Starting Network Thread...");
#endif

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
				catch (Exception)
				{
					s_eventLoopStarted = false;
					s_eventLoop = null;
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

#if DEBUG_THREADS
				Debug.WriteLine("Stopping Network Thread...");
#endif

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
							// TODO: logging ?
							Trace.WriteLine(String.Format("The fdb network thread has not stopped after {0} seconds. Forcing shutdown...", duration.Elapsed.TotalSeconds.ToString("N0")));

							// Force a shutdown
							thread.Abort();

							bool stopped = thread.Join(TimeSpan.FromSeconds(30));
							//REVIEW: is this even usefull? If the thread is stuck in a native P/Invoke call, it won't get notified until it returns to managed code ...
							// => in that case, we have a zombie thread on our hands...

							if (!stopped)
							{
								//TODO: logging?
								Trace.WriteLine(String.Format("The fdb network thread failed to stop after more than {0} seconds. Transaction integrity may not be guaranteed.", duration.Elapsed.TotalSeconds.ToString("N0")));
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
							//TODO: logging?
							Trace.WriteLine(String.Format("The fdb network thread took a long time to stop ({0} seconds).", duration.Elapsed.TotalSeconds.ToString("N0")));
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
#if DEBUG_THREADS
				Debug.WriteLine("FDB Event Loop running on thread #" + s_eventLoopThreadId.Value + "...");
#endif

				var err = FdbNative.RunNetwork();
				if (err != FdbError.Success)
				{ // Stop received
					//TODO: logging ?
					Trace.WriteLine(String.Format("The fdb network thread returned with error code {0}: {1}", err, GetErrorMessage(err)));
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
#if DEBUG_THREADS
				Debug.WriteLine("FDB Event Loop stopped");
#endif
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
		internal static void EnsureNotOnNetworkThread()
		{
#if DEBUG_THREADS
			Debug.WriteLine("> [Executing on thread " + Thread.CurrentThread.ManagedThreadId + "]");
#endif

			if (Fdb.IsNetworkThread)
			{ // cannot commit from same thread as the network loop because it could lead to a deadlock
				FailCannotExecuteOnNetworkThread();
			}
		}

		private static void FailCannotExecuteOnNetworkThread()
		{
#if DEBUG
			if (Debugger.IsAttached) Debugger.Break();
#endif
			throw new InvalidOperationException("Cannot commit transaction from the Network Thread!");
		}

		#endregion

		#region Cluster...

		/// <summary>Opens a connection to the local FDB cluster</summary>
		/// <param name="ct"></param>
		/// <returns></returns>
		public static Task<FdbCluster> OpenLocalClusterAsync(CancellationToken ct = default(CancellationToken))
		{
			//BUGBUG: does 'null' means Local? or does it mean the default config file that may or may not point to the local cluster ??
			return OpenClusterAsync(null, ct);
		}

		/// <summary>Opens a connection to an FDB Cluster</summary>
		/// <param name="path">Path to the 'fdb.cluster' file, or null for default</param>
		/// <returns>Task that will return an FdbCluster, or an exception</returns>
		public static Task<FdbCluster> OpenClusterAsync(string path = null, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			EnsureIsStarted();

			Debug.WriteLine("Connecting to " + (path == null ? "default cluster" : ("cluster specified in " + path)));

			//TODO: check path ?
			var future = FdbNative.CreateCluster(path);

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
					return new FdbCluster(cluster, path);
				},
				ct
			);
		}

		#endregion

		#region Database...

		/// <summary>Open a database on the specified cluster</summary>
		/// <param name="path">Path to the 'fdb.cluster' file, or null for default</param>
		/// <param name="name">Name of the database. Must be 'DB'</param>
		/// <param name="ct">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of Beta2, the only supported database name is 'DB'</remarks>
		/// <exception cref="System.InvalidOperationException">If <paramref name="name"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		public static async Task<FdbDatabase> OpenDatabaseAsync(string path, string name, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			Debug.WriteLine("Connecting to database " + name + " on cluster " + path + " ...");

			FdbCluster cluster = null;
			FdbDatabase db = null;
			bool success = false;
			try
			{
				cluster = await OpenClusterAsync(path, ct).ConfigureAwait(false);
				//note: since the cluster is not provided by the caller, link it with the database's Dispose()
				db = await cluster.OpenDatabaseAsync(name, true, ct).ConfigureAwait(false);
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


		/// <summary>Open a database on the local cluster</summary>
		/// <param name="name">Name of the database. Must be 'DB'</param>
		/// <param name="ct">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of Beta2, the only supported database name is 'DB'</remarks>
		/// <exception cref="System.InvalidOperationException">If <paramref name="name"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		public static async Task<FdbDatabase> OpenLocalDatabaseAsync(string name, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			Debug.WriteLine("Connecting to local database " + name + " ...");

			FdbCluster cluster = null;
			FdbDatabase db = null;
			bool success = false;
			try
			{
				cluster = await OpenLocalClusterAsync(ct).ConfigureAwait(false);
				//note: since the cluster is not provided by the caller, link it with the database's Dispose()
				db = await cluster.OpenDatabaseAsync(name, true, ct).ConfigureAwait(false);
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
			s_started = true;

			// register with the AppDomain to ensure that everyting is cleared when the process exists
			s_appDomainUnloadHandler = (sender, args) =>
			{
				if (s_started)
				{
					Debug.WriteLine("AppDomain is unloading, stopping FoundationDB Network Thread...");
					Stop();
				}
			};
			AppDomain.CurrentDomain.DomainUnload += s_appDomainUnloadHandler;
			//TODO: should we also register with AppDomain.ProcessExit event ?


			Debug.WriteLine("Selecting API version " + FdbNative.FDB_API_VERSION);

			DieOnError(FdbNative.SelectApiVersion(FdbNative.FDB_API_VERSION));

			Debug.WriteLine("Setting up Network Thread...");

			if (Fdb.Options.TracePath != null)
			{
				Debug.WriteLine("Will trace client activity in " + Fdb.Options.TracePath);
				// create trace directory if missing...
				if (!Directory.Exists(Fdb.Options.TracePath)) Directory.CreateDirectory(Fdb.Options.TracePath);

				unsafe
				{
					var data = FdbNative.ToNativeString(Fdb.Options.TracePath, nullTerminated: true);
					fixed (byte* ptr = data.Array)
					{
						DieOnError(FdbNative.NetworkSetOption(FdbNetworkOption.TraceEnable, ptr + data.Offset, data.Count));
					}
				}
			}

			DieOnError(FdbNative.SetupNetwork());
			Debug.WriteLine("Network has been set up");

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

				Debug.WriteLine("Stopping Network Thread");
				StopEventLoop();
				Debug.WriteLine("Stopped");
			}
		}

	}

}
