#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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

//#define DEBUG_THREADS

namespace FoundationDB.Client
{
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using SystemIO = System.IO;

	/// <summary>FoundationDB binding</summary>
	[PublicAPI]
	public static partial class Fdb
	{

		#region Constants...

		/// <summary>Keys cannot exceed 10,000 bytes</summary>
		internal const int MaxKeySize = 10 * 1000;

		/// <summary>Values cannot exceed 100,000 bytes</summary>
		internal const int MaxValueSize = 100 * 1000;

		/// <summary>Maximum size of total written keys and values by a transaction</summary>
		internal const int MaxTransactionWriteSize = 10 * 1024 * 1024;

		/// <summary>Minimum API version supported by this binding</summary>
		internal const int MinSafeApiVersion = FdbNative.FDB_API_MIN_VERSION;

		/// <summary>Highest API version that this binding can support</summary>
		/// <remarks>Ex: this binding has been tested against v3.x (300) but the installed client can be v4.x (400).</remarks>
		internal const int MaxSafeApiVersion = FdbNative.FDB_API_MAX_VERSION;

		/// <summary>Default API version that will be selected, if the application does not specify otherwise.</summary>
		internal const int DefaultApiVersion = 300; // v3.0.x
		//INVARIANT: MinSafeApiVersion <= DefaultApiVersion <= MaxSafeApiVersion

		#endregion

		#region Members...

		/// <summary>Flag indicating if FDB has been initialized or not</summary>
		private static bool s_started; //REVIEW: replace with state flags (Starting, Started, Failed, ...)

		/// <summary>Currently selected API version</summary>
		private static int s_apiVersion = DefaultApiVersion;

		/// <summary>Event handler called when the AppDomain gets unloaded</summary>
		private static EventHandler s_appDomainUnloadHandler;

		internal static readonly byte[] EmptyArray = new byte[0];
		//TODO: move this somewhere else (Slice?)

		#endregion

		/// <summary>Returns the minimum API version currently supported by this binding.</summary>
		/// <remarks>Attempts to select an API version lower than this value will fail.</remarks>
		public static int GetMinApiVersion()
		{
			return MinSafeApiVersion;
		}

		/// <summary>Returns the maximum API version currently supported by the installed client.</summary>
		/// <remarks>The version of the installed client (fdb_c.dll) can be different higher (or lower) than the version supported by this binding (FoundationDB.Client.dll)!
		/// If you want the highest possible version that is supported by both the binding and the client, you must call <see cref="GetMaxSafeApiVersion"/>.
		/// Attempts to select an API version higher than this value will fail.
		/// </remarks>
		public static int GetMaxApiVersion()
		{
			return FdbNative.GetMaxApiVersion();
		}

		/// <summary>Returns the maximum API version that is supported by both this binding and the installed client.</summary>
		/// <returns>Value that can be safely passed to <see cref="UseApiVersion"/> or <see cref="Start(int)"/>, if you want to be on the bleeding edge.</returns>
		/// <remarks>This value can be lower than the value returned by <see cref="GetMaxApiVersion"/> if the FoundationDB client installed on this machine is more recent that the version of this assembly.
		/// Using this version may break your application if new features change the behavior of the client (ex: default mode for snapshot transactions between v2.x and v3.x).
		/// </remarks>
		public static int GetMaxSafeApiVersion()
		{
			return Math.Min(MaxSafeApiVersion, GetMaxApiVersion());
		}

		/// <summary>Returns the currently selected API version.</summary>
		/// <remarks>Unless explicitely selected by calling <see cref="UseApiVersion"/> before, the default API version level will be returned</remarks>
		public static int ApiVersion
		{
			get { return s_apiVersion; }
		}

		/// <summary>Sets the desired API version of the binding.
		/// The selected version level may affect the availability and behavior or certain features.
		/// </summary>
		/// <remarks>
		/// The version can only be set before calling <see cref="Fdb.Start()"/> or any method that indirectly calls it.
		/// If you want to be on the bleeding edge, you can use <see cref="GetMaxSafeApiVersion"/> to get the maximum version supported by both this bindign and the FoundationDB client.
		/// If you want to be conservative, you should target a specific version level, and only change to newer versions after making sure that all tests are passing!
		/// </remarks>
		/// <exception cref="InvalidOperationException">When attempting to change the API version after the binding has been started.</exception>
		/// <exception cref="ArgumentException">When attempting to set a negative version, or a version that is either less or greater than the minimum and maximum supported versions.</exception>
		public static void UseApiVersion(int value)
		{
			if (value < 0) throw new ArgumentException("API version must be a positive integer.");
			if (value == 0)
			{ // 0 means "use the default version"
				value = DefaultApiVersion;
			}
			if (s_apiVersion == value) return; //Alreay set to same version... skip it.
			if (s_started) throw new InvalidOperationException(string.Format("You cannot set API version {0} because version {1} has already been selected", value, s_apiVersion));

			//note: we don't actually select the version yet, only when Start() is called.

			int min = GetMinApiVersion();
			if (value < min) throw new ArgumentException(String.Format("The minimum API version supported by this binding is {0} and the default version is {1}.", min, DefaultApiVersion));
			int max = GetMaxApiVersion();
			if (value > max) throw new ArgumentException(String.Format("The maximum API version supported by this binding is {0} and the default version is {1}.", max, DefaultApiVersion));

			s_apiVersion = value;
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
		private static bool s_eventLoopStopRequested;
		private static int? s_eventLoopThreadId;

		/// <summary>Starts the thread running the FDB event loop</summary>
		private static void StartEventLoop()
		{
			if (s_eventLoop == null)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "StartEventLoop", "Starting network thread...");

				s_eventLoopStarted = false;
				s_eventLoopStopRequested = false;
				s_eventLoopRunning = false;

				var thread = new Thread(EventLoop)
				{
					Name = "FdbNetworkLoop",
					IsBackground = true,
					Priority = ThreadPriority.AboveNormal
				};
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

				s_eventLoopStopRequested = true;

				var err = FdbNative.StopNetwork();
				if (err != FdbError.Success)
				{
					if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", String.Format("Failed to stop event loop: {0}", err.ToString()));
				}
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
		[HandleProcessCorruptedStateExceptions]
		private static void EventLoop()
		{
			//TODO: we need to move the crash handling logic outside this method, so that an app can hook up an event and device what to do: crash or keep running (dangerous!).
			// At least, the app would get the just to do some emergency shutdown logic before letting the process die....

			try
			{
				s_eventLoopRunning = true;

				s_eventLoopThreadId = Thread.CurrentThread.ManagedThreadId;

				if (Logging.On) Logging.Verbose(typeof(Fdb), "EventLoop", String.Format("FDB Event Loop running on thread #{0}...", s_eventLoopThreadId.Value));

				var err = FdbNative.RunNetwork();
				if (err != FdbError.Success)
				{
					if (s_eventLoopStopRequested || Environment.HasShutdownStarted)
					{ // this was requested, or can be explained by the computer shutting down...
						if (Logging.On) Logging.Info(typeof(Fdb), "EventLoop", String.Format("The fdb network thread returned with error code {0}: {1}", err, GetErrorMessage(err)));
					}
					else
					{ // this was NOT expected !
						if (Logging.On) Logging.Error(typeof(Fdb), "EventLoop", String.Format("The fdb network thread returned with error code {0}: {1}", err, GetErrorMessage(err)));
#if DEBUG
						Console.Error.WriteLine("THE FDB NETWORK EVENT LOOP HAS FAILED!");
						Console.Error.WriteLine("=> " + err);
						// REVIEW: should we FailFast in release mode also?
						// => this may be a bit suprising for most users when applications unexpectedly crash for for no apparent reason.
						Environment.FailFast("The FoundationDB Network Event Loop failed with error " + err + " and was terminated.");
#endif
					}
				}
			}
			catch (Exception e)
			{
				if (e is ThreadAbortException)
				{ // some other thread tried to Abort() us. This probably means that we should exit ASAP...
					Thread.ResetAbort();
					return;
				}

				//note: any error is this thread is BAD NEWS for the process, the the network thread usually cannot be restarted safely.

				if (e is AccessViolationException)
				{
					// An access violation occured inside the native code. This good be caused by:
					// - a bug in fdb_c.dll
					// - a bug in our own marshalling code that calls into fdb_c.dll
					// - some other random heap corruption that caused us to pass bogus data to fdb_c.dll
					// - a random cosmic ray that flipped some bits in memory...
					if (Debugger.IsAttached) Debugger.Break();

					// This error is VERY BAD NEWS, and means that we CANNOT continue safely running fdb in this process
					// The only reasonable option is to exit the process immediately !

					Console.Error.WriteLine("THE FDB NETWORK EVENT LOOP HAS CRASHED!");
					Console.Error.WriteLine("=> " + e.ToString());
					Environment.FailFast("The FoundationDB Network Event Loop crashed with an Access Violation, and had to be terminated. You may try to create full memory dumps, as well as attach a debugger to this process (it will automatically break when this problem occurs).", e);
					return;
				}

				if (Logging.On) Logging.Exception(typeof(Fdb), "EventLoop", e);

#if DEBUG
				// if we are running in DEBUG build, we want to get the attention of the developper on this.
				// the best way is to make the test runner explode in mid-air with a scary looking message!

				Console.Error.WriteLine("THE FDB NETWORK EVENT LOOP HAS CRASHED!");
				Console.Error.WriteLine("=> " + e.ToString());
				// REVIEW: should we FailFast in release mode also?
				// => this may be a bit suprising for most users when applications unexpectedly crash for for no apparent reason.
				Environment.FailFast("The FoundationDB Network Event Loop crashed and had to be terminated: " + e.Message, e);
#endif
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

		[ContractAnnotation("=> halt")]
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
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbCluster, or an exception</returns>
		[ItemNotNull]
		public static Task<IFdbCluster> CreateClusterAsync(CancellationToken cancellationToken)
		{
			return CreateClusterAsync(null, cancellationToken);
		}

		/// <summary>Opens a connection to an existing FDB Cluster</summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbCluster, or an exception</returns>
		[ItemNotNull]
		public static async Task<IFdbCluster> CreateClusterAsync(string clusterFile, CancellationToken cancellationToken)
		{
			return await CreateClusterInternalAsync(clusterFile, cancellationToken).ConfigureAwait(false);
		}

		[ItemNotNull]
		private static async Task<FdbCluster> CreateClusterInternalAsync(string clusterFile, CancellationToken cancellationToken)
		{
			EnsureIsStarted();

			// "" should also be considered to mean "default cluster file"
			if (string.IsNullOrEmpty(clusterFile)) clusterFile = null;

			if (Logging.On) Logging.Info(typeof(Fdb), "CreateClusterAsync", clusterFile == null ? "Connecting to default cluster..." : String.Format("Connecting to cluster using '{0}' ...", clusterFile));

			if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

			//TODO: check the path ? (exists, readable, ...)

			//TODO: have a way to configure the default IFdbClusterHander !
			var handler = await FdbNativeCluster.CreateClusterAsync(clusterFile, cancellationToken).ConfigureAwait(false);
			return new FdbCluster(handler, clusterFile);
		}

		#endregion

		#region Database...

		/// <summary>Create a new connection with the "DB" database on the cluster specified by the default cluster file.</summary>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>Since connections are not pooled, so this method can be costly and should NOT be called every time you need to read or write from the database. Instead, you should open a database instance at the start of your process, and use it a singleton.</remarks>
		[ItemNotNull]
		public static Task<IFdbDatabase> OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return OpenAsync(clusterFile: null, dbName: null, globalSpace: FdbSubspace.Empty, cancellationToken: cancellationToken);
		}

		/// <summary>Create a new connection with the "DB" database on the cluster specified by the default cluster file, and with the specified global subspace</summary>
		/// <param name="globalSpace">Global subspace used as a prefix for all keys and layers</param>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>Since connections are not pooled, so this method can be costly and should NOT be called every time you need to read or write from the database. Instead, you should open a database instance at the start of your process, and use it a singleton.</remarks>
		[ItemNotNull]
		public static Task<IFdbDatabase> OpenAsync(IFdbSubspace globalSpace, CancellationToken cancellationToken = default(CancellationToken))
		{
			return OpenAsync(clusterFile: null, dbName: null, globalSpace: globalSpace, cancellationToken: cancellationToken);
		}

		/// <summary>Create a new connection with a database on the specified cluster</summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="dbName">Name of the database, or "DB" if not specified.</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of 1.0, the only supported database name is "DB"</remarks>
		/// <exception cref="InvalidOperationException">If <paramref name="dbName"/> is anything other than "DB"</exception>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>Since connections are not pooled, so this method can be costly and should NOT be called every time you need to read or write from the database. Instead, you should open a database instance at the start of your process, and use it a singleton.</remarks>
		[ItemNotNull]
		public static Task<IFdbDatabase> OpenAsync(string clusterFile, string dbName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return OpenAsync(clusterFile, dbName, FdbSubspace.Empty, readOnly: false, cancellationToken: cancellationToken);
		}

		/// <summary>Create a new connection with a database on the specified cluster</summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="dbName">Name of the database. Must be 'DB'</param>
		/// <param name="globalSpace">Global subspace used as a prefix for all keys and layers</param>
		/// <param name="readOnly">If true, the database instance will only allow read operations</param>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of 1.0, the only supported database name is 'DB'</remarks>
		/// <exception cref="InvalidOperationException">If <paramref name="dbName"/> is anything other than 'DB'</exception>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>Since connections are not pooled, so this method can be costly and should NOT be called every time you need to read or write from the database. Instead, you should open a database instance at the start of your process, and use it a singleton.</remarks>
		[ItemNotNull]
		public static async Task<IFdbDatabase> OpenAsync(string clusterFile, string dbName, IFdbSubspace globalSpace, bool readOnly = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			return await OpenInternalAsync(clusterFile, dbName, globalSpace, readOnly, cancellationToken);
		}

		/// <summary>Create a new database handler instance using the specificied cluster file, database name, global subspace and read only settings</summary>
		[ItemNotNull]
		internal static async Task<FdbDatabase> OpenInternalAsync(string clusterFile, string dbName, IFdbSubspace globalSpace, bool readOnly, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			dbName = dbName ?? "DB";
			globalSpace = globalSpace ?? FdbSubspace.Empty;

			if (Logging.On) Logging.Info(typeof(Fdb), "OpenAsync", String.Format("Connecting to database '{0}' using cluster file '{1}' and subspace '{2}' ...", dbName, clusterFile, globalSpace));

			FdbCluster cluster = null;
			FdbDatabase db = null;
			bool success = false;
			try
			{
				cluster = await CreateClusterInternalAsync(clusterFile, cancellationToken).ConfigureAwait(false);
				//note: since the cluster is not provided by the caller, link it with the database's Dispose()
				db = await cluster.OpenDatabaseInternalAsync(dbName, globalSpace, readOnly: readOnly, ownsCluster: true, cancellationToken: cancellationToken).ConfigureAwait(false);
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

		/// <summary>Select the API version level to use in this process, and start the Network Thread</summary>
		public static void Start(int apiVersion)
		{
			UseApiVersion(apiVersion);
			Start();
		}

		/// <summary>Start the Network Thread, using the currently selected API version level</summary>
		/// <remarks>If you need a specific API version level, it must be defined by either calling <see cref="UseApiVersion"/> before calling this method, or by using the <see cref="Start(int)"/> override. Otherwise, the default API version will be selected.</remarks>
		public static void Start()
		{
			if (s_started) return;

			//BUGBUG: Specs say we cannot restart the network thread anymore in the process after stoping it ! :(

			s_started = true;

			int apiVersion = s_apiVersion;
			if (apiVersion <= 0) apiVersion = DefaultApiVersion;

			if (Logging.On) Logging.Info(typeof(Fdb), "Start", String.Format("Selecting fdb API version {0}", apiVersion));

			FdbError err = FdbNative.SelectApiVersion(apiVersion);
			if (err != FdbError.Success)
			{
				if (Logging.On) Logging.Error(typeof(Fdb), "Start", String.Format("Failed to fdb API version {0}: {1}", apiVersion, err));

				switch (err)
				{
					case FdbError.ApiVersionNotSupported:
					{ // bad version was selected ?
						// note: we already bound check the values before, so that means that fdb_c.dll is either an older version or an incompatible new version.
						throw new FdbException(err, String.Format("The API version {0} is not supported by the FoundationDB client library (fdb_c.dll) installed on this system. The binding only supports versions {1} to {2}. You either need to upgrade the .NET binding or the FoundationDB client library to a newer version.", apiVersion, GetMinApiVersion(), GetMaxApiVersion()));
					}
#if DEBUG
					case FdbError.ApiVersionAlreadySet:
					{ // Temporary hack to allow multiple debugging using the cached host process in VS
						Console.Error.WriteLine("FATAL: CANNOT REUSE EXISTING PROCESS! FoundationDB client cannot be restarted once stopped. Current process will be terminated.");
						Environment.FailFast("FATAL: CANNOT REUSE EXISTING PROCESS! FoundationDB client cannot be restarted once stopped. Current process will be terminated.");
						break;
					}
#endif
				}
				DieOnError(err);
			}
			s_apiVersion = apiVersion;

			if (!string.IsNullOrWhiteSpace(Fdb.Options.TracePath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will trace client activity in '{0}'", Fdb.Options.TracePath));
				// create trace directory if missing...
				if (!SystemIO.Directory.Exists(Fdb.Options.TracePath)) SystemIO.Directory.CreateDirectory(Fdb.Options.TracePath);

				DieOnError(SetNetworkOption(FdbNetworkOption.TraceEnable, Fdb.Options.TracePath));
			}

			if (!string.IsNullOrWhiteSpace(Fdb.Options.TLSPlugin))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will use custom TLS plugin '{0}'", Fdb.Options.TLSPlugin));

				DieOnError(SetNetworkOption(FdbNetworkOption.TLSPlugin, Fdb.Options.TLSPlugin));
			}

			if (Fdb.Options.TLSCertificateBytes.IsPresent)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will load TLS root certificate and private key from memory ({0} bytes)", Fdb.Options.TLSCertificateBytes.Count));

				DieOnError(SetNetworkOption(FdbNetworkOption.TLSCertBytes, Fdb.Options.TLSCertificateBytes));
			}
			else if (!string.IsNullOrWhiteSpace(Fdb.Options.TLSCertificatePath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will load TLS root certificate and private key from '{0}'", Fdb.Options.TLSCertificatePath));

				DieOnError(SetNetworkOption(FdbNetworkOption.TLSCertPath, Fdb.Options.TLSCertificatePath));
			}

			if (Fdb.Options.TLSPrivateKeyBytes.IsPresent)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will load TLS private key from memory ({0} bytes)", Fdb.Options.TLSPrivateKeyBytes.Count));

				DieOnError(SetNetworkOption(FdbNetworkOption.TLSKeyBytes, Fdb.Options.TLSPrivateKeyBytes));
			}
			else if (!string.IsNullOrWhiteSpace(Fdb.Options.TLSPrivateKeyPath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will load TLS private key from '{0}'", Fdb.Options.TLSPrivateKeyPath));

				DieOnError(SetNetworkOption(FdbNetworkOption.TLSKeyPath, Fdb.Options.TLSPrivateKeyPath));
			}

			if (Fdb.Options.TLSVerificationPattern.IsPresent)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", String.Format("Will verify TLS peers with pattern '{0}'", Fdb.Options.TLSVerificationPattern));

				DieOnError(SetNetworkOption(FdbNetworkOption.TLSVerifyPeers, Fdb.Options.TLSVerificationPattern));
			}

			try { }
			finally
			{
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

				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", "Setting up Network Thread...");

				DieOnError(FdbNative.SetupNetwork());
				s_started = true; //BUGBUG: already set at the start of the method. Maybe we need state flags ?
			}

			if (Logging.On) Logging.Info(typeof(Fdb), "Start", "Network thread has been set up");

			StartEventLoop();
		}

		/// <summary>Set the value of a network option on the database handler</summary>
		private static FdbError SetNetworkOption(FdbNetworkOption option, string value)
		{
			unsafe
			{
				var data = FdbNative.ToNativeString(value, nullTerminated: false);
				fixed (byte* ptr = data.Array)
				{
					return FdbNative.NetworkSetOption(option, ptr + data.Offset, data.Count);
				}
			}
		}

		/// <summary>Set the value of a network option on the database handler</summary>
		private static FdbError SetNetworkOption(FdbNetworkOption option, Slice value)
		{
			SliceHelpers.EnsureSliceIsValid(ref value);
			unsafe
			{
				fixed (byte* ptr = value.Array)
				{
					return FdbNative.NetworkSetOption(option, ptr + value.Offset, value.Count);
				}
			}
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
