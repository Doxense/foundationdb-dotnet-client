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
using FoundationDb.Client.Native;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client
{

	public static partial class FdbCore
	{

		public static string NativeLibPath = ".";
		public static string TracePath = null;

		internal static readonly ArraySegment<byte> Empty = new ArraySegment<byte>(new byte[0]);

		/// <summary>Keys cannot exceed 10,000 bytes</summary>
		internal const int MaxKeySize = 10 * 1000;

		/// <summary>Values cannot exceed 100,000 bytes</summary>
		internal const int MaxValueSize = 100 * 1000;

		/// <summary>Maximum size of total written keys and values by a transaction</summary>
		internal const int MaxTransactionWriteSize = 10 * 1024 * 1024;

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

		#region Key/Value serialization

		internal static ArraySegment<byte> GetKeyBytes(string key)
		{
			if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty.", "key");
			return new ArraySegment<byte>(Encoding.UTF8.GetBytes(key));
		}

		internal static void EnsureKeyIsValid(ArraySegment<byte> key)
		{
			if (key.Count == 0) throw new ArgumentException("Key cannot be null or empty.", "key");
			if (key.Count > FdbCore.MaxKeySize) throw new ArgumentException(String.Format("Key is too big ({0} > {1}).", key.Count, FdbCore.MaxKeySize), "key");
		}

		internal static ArraySegment<byte> GetValueBytes(string value)
		{
			if (value == null) throw new ArgumentNullException("Value cannot be null.", "value");
			if (value.Length == 0) return FdbCore.Empty;
			return new ArraySegment<byte>(Encoding.UTF8.GetBytes(value));
		}

		internal static void EnsureValueIsValid(ArraySegment<byte> value)
		{
			if (value.Count == 0) throw new ArgumentNullException("value cannot be null.", "value");
			if (value.Count > FdbCore.MaxValueSize) throw new ArgumentException(String.Format("Value is too big ({0} > {1}).", value.Count, FdbCore.MaxValueSize), "value");
		}

		#endregion

		#region Network Event Loop...

		private static Thread s_eventLoop;
		private static bool s_eventLoopStarted;
		private static bool s_eventLoopRunning;
		private static int? s_eventLoopThreadId;

		private static void StartEventLoop()
		{
			if (s_eventLoop == null)
			{
				Debug.WriteLine("Starting event loop...");

				var thread = new Thread(new ThreadStart(EventLoop));
				thread.Name = "FoundationDB Event Loop";
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

		private static void StopEventLoop()
		{
			if (s_eventLoopStarted)
			{
				var err = FdbNativeStub.StopNetwork();
				s_eventLoopStarted = false;

				var thread = s_eventLoop;
				if (thread != null && thread.IsAlive)
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
				s_eventLoopRunning = true;

				s_eventLoopThreadId = Thread.CurrentThread.ManagedThreadId;
				Debug.WriteLine("Event Loop running on thread #" + s_eventLoopThreadId.Value + "...");

				var err = FdbNativeStub.RunNetwork();
				if (err != FdbError.Success)
				{ // Stop received
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
				s_eventLoopThreadId = null;
				s_eventLoopRunning = false;
			}
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

		internal static void EnsureNotOnNetworkThread()
		{
#if DEBUG
			Debug.WriteLine("[Executing on thread " + Thread.CurrentThread.ManagedThreadId + "]");
#endif

			if (FdbCore.IsNetworkThread)
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

		public static Task<FdbCluster> OpenLocalClusterAsync(CancellationToken ct = default(CancellationToken))
		{
			return OpenClusterAsync(null, ct);
		}

		/// <summary>Asynchronously return a connection to a FDB Cluster</summary>
		/// <param name="path">Path to the 'fdb.cluster' file, or null for default</param>
		/// <returns></returns>
		public static Task<FdbCluster> OpenClusterAsync(string path = null, CancellationToken ct = default(CancellationToken))
		{
			//TODO: check path
			var future = FdbNativeStub.CreateCluster(path);

			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					ClusterHandle cluster;
					var err = FdbNativeStub.FutureGetCluster(h, out cluster);
					if (err != FdbError.Success)
					{
						cluster.Dispose();
						throw MapToException(err);
					}
					return new FdbCluster(cluster);
				},
				ct
			);
		}

		#endregion

		private static void EnsureIsStarted()
		{
			if (!s_eventLoopStarted) Start();
		}

		public static void Start()
		{
			Debug.WriteLine("Selecting API version " + FdbNativeStub.FDB_API_VERSION);
			DieOnError(FdbNativeStub.SelectApiVersion(FdbNativeStub.FDB_API_VERSION));

			Debug.WriteLine("Setting up network...");

			if (TracePath != null)
			{
				Debug.WriteLine("Will trace network activity in " + TracePath);
				// create trace directory if missing...
				if (!Directory.Exists(TracePath)) Directory.CreateDirectory(TracePath);

				unsafe
				{
					var data = FdbNativeStub.ToNativeString(TracePath, nullTerminated: true);
					fixed (byte* ptr = data)
					{
						DieOnError(FdbNativeStub.NetworkSetOption(FdbNetworkOption.TraceEnable, ptr, data.Length));
					}
				}
			}

			DieOnError(FdbNativeStub.SetupNetwork());
			Debug.WriteLine("Network has been set up");

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
			EnsureIsStarted();

			Debug.WriteLine("Connecting to cluster... " + clusterPath);
			return OpenClusterAsync(clusterPath);
		}

	}

}
