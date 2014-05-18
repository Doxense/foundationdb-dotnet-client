#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using SystemIO = System.IO;

	/// <summary>FoundationDB binding</summary>
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
		internal const int MinimumApiVersion = 200; // v2.0.x

		#endregion

		#region Members...

		/// <summary>Flag indicating if FDB has been initialized or not</summary>
		private static bool s_started; //REVIEW: replace with state flags (Starting, Started, Failed, ...)

		/// <summary>Currently selected API version</summary>
		private static int s_apiVersion = FdbNative.FDB_API_VERSION;

		/// <summary>Event handler called when the AppDomain gets unloaded</summary>
		private static EventHandler s_appDomainUnloadHandler;

		internal static readonly byte[] EmptyArray = new byte[0];
		//TODO: move this somewhere else (Slice?)

		#endregion

		/// <summary>Returns the minimum API version currently supported by this binding</summary>
		public static int GetMinApiVersion()
		{
			return MinimumApiVersion;
		}

		/// <summary>Returns the maximum API version currently supported by this binding</summary>
		public static int GetMaxApiVersion()
		{
			return FdbNative.GetMaxApiVersion();
		}

		/// <summary>Returns the currently selected API version</summary>
		/// <remarks>Unless explicitely selected by calling <see cref="UseApiVersion"/> before, the default API version level will be returned</remarks>
		public static int ApiVersion
		{
			get { return s_apiVersion; }
		}

		/// <summary>Sets the desired API version of the binding</summary>
		/// <remarks>The version can only be set before calling <see cref="Fdb.Start()"/> or any method that indirectly calls it.</remarks>
		/// <exception cref="InvalidOperationException">When attempting to change the API version after the binding has been started.</exception>
		/// <exception cref="ArgumentException">When attempting to set a negative version, or a version that is either less or greater than the minimum and maximum supported versions.</exception>
		public static void UseApiVersion(int value)
		{
			if (s_started) throw new InvalidOperationException("You cannot change the API Version after Fdb.Start() has been called.");
			if (value < 0) throw new ArgumentException("API version must be a positive integer.");

			//note: we don't actually select the version yet, only when Start() is called.

			if (value == 0)
			{ // 0 means "use the default version"
				s_apiVersion = FdbNative.FDB_API_VERSION;
			}
			else
			{
				int min = GetMinApiVersion();
				if (value < min) throw new ArgumentException(String.Format("The minimum API version supported by this binding is {0} and the default version is {1}.", min, FdbNative.FDB_API_VERSION));
				int max = GetMaxApiVersion();
				if (value > max) throw new ArgumentException(String.Format("The maximum API version supported by this binding is {0} and the default version is {1}.", max, FdbNative.FDB_API_VERSION));

				s_apiVersion = value;
			}
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

				var thread = new Thread(EventLoop);
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
		private static void EventLoop()
		{
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
						Console.Error.WriteLine("THE FDB NETWORK EVENT LOOP HAS CRASHED! PLEASE RESTART THE PROCESS!");
						Console.Error.WriteLine("=> " + err);
						// REVIEW: should we FailFast in release mode also?
						// => this may be a bit suprising for most users when applications unexpectedly crash for for no apparent reason.
						Environment.FailFast("FoundationDB network event loop failed with error " + err);
#endif
					}
				}
			}
			catch (Exception e)
			{
				if (e is ThreadAbortException)
				{ // bye bye
					Thread.ResetAbort();
					return;
				}
				//TODO: logging ?
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
		public static Task<IFdbCluster> CreateClusterAsync(CancellationToken cancellationToken)
		{
			return CreateClusterAsync(null, cancellationToken);
		}

		/// <summary>Opens a connection to an existing FDB Cluster</summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbCluster, or an exception</returns>
		public static async Task<IFdbCluster> CreateClusterAsync(string clusterFile, CancellationToken cancellationToken)
		{
			return await CreateClusterInternalAsync(clusterFile, cancellationToken).ConfigureAwait(false);
		}

		public static Task<FdbCluster> CreateClusterInternalAsync(string clusterFile, CancellationToken cancellationToken)
		{
			EnsureIsStarted();

			// "" should also be considered to mean "default cluster file"
			if (string.IsNullOrEmpty(clusterFile)) clusterFile = null;

			if (Logging.On) Logging.Info(typeof(Fdb), "CreateClusterAsync", clusterFile == null ? "Connecting to default cluster..." : String.Format("Connecting to cluster using '{0}' ...", clusterFile));

			if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

			//TODO: check the path ? (exists, readable, ...)

			//TODO: have a way to configure the default IFdbClusterHander !
			return FdbNativeCluster.CreateClusterAsync(clusterFile, cancellationToken);
		}

		#endregion

		#region Database...

		/// <summary>
		/// Open the "DB" database on the cluster specified by the default cluster file
		/// </summary>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		public static Task<IFdbDatabase> OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return OpenAsync(clusterFile: null, dbName: null, globalSpace: FdbSubspace.Empty, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Open the "DB" database on the cluster specified by the default cluster file, and with the specified global space
		/// </summary>
		/// <param name="globalSpace">Global subspace used as a prefix for all keys and layers</param>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		public static Task<IFdbDatabase> OpenAsync(FdbSubspace globalSpace, CancellationToken cancellationToken = default(CancellationToken))
		{
			return OpenAsync(clusterFile: null, dbName: null, globalSpace: globalSpace, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Open a database on the specified cluster
		/// </summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="dbName">Name of the database, or "DB" if not specified.</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of 1.0, the only supported database name is "DB"</remarks>
		/// <exception cref="InvalidOperationException">If <paramref name="dbName"/> is anything other than "DB"</exception>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		public static Task<IFdbDatabase> OpenAsync(string clusterFile, string dbName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return OpenAsync(clusterFile, dbName, FdbSubspace.Empty, readOnly: false, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Open a database on the specified cluster
		/// </summary>
		/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
		/// <param name="dbName">Name of the database. Must be 'DB'</param>
		/// <param name="globalSpace">Global subspace used as a prefix for all keys and layers</param>
		/// <param name="readOnly">If true, the database instance will only allow read operations</param>
		/// <param name="cancellationToken">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <remarks>As of 1.0, the only supported database name is 'DB'</remarks>
		/// <exception cref="InvalidOperationException">If <paramref name="dbName"/> is anything other than 'DB'</exception>
		/// <exception cref="OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		public static async Task<IFdbDatabase> OpenAsync(string clusterFile, string dbName, FdbSubspace globalSpace, bool readOnly = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			return await OpenInternalAsync(clusterFile, dbName, globalSpace, readOnly, cancellationToken);
		}

		internal static async Task<FdbDatabase> OpenInternalAsync(string clusterFile, string dbName, FdbSubspace globalSpace, bool readOnly, CancellationToken cancellationToken)
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

		/// <summary>Select the correct API version, and start the Network Thread</summary>
		/// <remarks>If you need a specific API version level, it must be defined by calling <see cref="UseApiVersion"/> before calling this method, otherwise the default API version will be selected.</remarks>
		public static void Start()
		{
			if (s_started) return;

			//BUGBUG: Specs say we cannot restart the network thread anymore in the process after stoping it ! :(

			s_started = true;

			int apiVersion = s_apiVersion;
			if (apiVersion <= 0) apiVersion = FdbNative.FDB_API_VERSION;

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
						Console.WriteLine("REUSING EXISTING PROCESS! IF THINGS BREAK IN WEIRD WAYS, PLEASE RESTART THE PROCESS!");
						err = FdbError.Success;
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
