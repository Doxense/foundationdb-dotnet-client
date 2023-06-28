#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

// enable this to capture the stacktrace of the ctor, when troubleshooting leaked database handles
//#define CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client.Core;

	/// <summary>Wraps a native FDBDatabase* handle</summary>
	[DebuggerDisplay("Handle={m_handle}, Closed={m_handle.IsClosed}")]
	internal sealed class FdbNativeDatabase : IFdbDatabaseHandler
	{
		/// <summary>Handle that wraps the native FDB_DATABASE*</summary>
		private readonly DatabaseHandle m_handle;

		/// <summary>Optional Cluster handle (only for API 600 or below)</summary>
		private readonly ClusterHandle? m_clusterHandle;

		/// <summary>Path to the cluster file</summary>
		private readonly string? m_clusterFile;

#if CAPTURE_STACKTRACES
		private readonly StackTrace m_stackTrace;
#endif

		public FdbNativeDatabase(DatabaseHandle handle, string? clusterFile, ClusterHandle? cluster = null)
		{
			Contract.NotNull(handle);

			m_handle = handle;
			m_clusterFile = clusterFile;
			m_clusterHandle = cluster;
#if CAPTURE_STACKTRACES
			m_stackTrace = new StackTrace();
#endif
		}

#if DEBUG
		// We add a destructor in DEBUG builds to help track leaks of databases...
		~FdbNativeDatabase()
		{
#if CAPTURE_STACKTRACES
			Trace.WriteLine("A database handle (" + m_handle + ") was leaked by " + m_stackTrace);
#endif
			// If you break here, that means that a native database handler was leaked by a FdbDatabase instance (or that the database instance was leaked)
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
			Dispose(false);
		}
#endif

		public static ValueTask<IFdbDatabaseHandler> CreateDatabaseAsync(string? clusterFile, CancellationToken ct)
		{
			if (Fdb.GetMaxApiVersion() < 610)
			{ // Older version used a different way to create a database handle
#pragma warning disable CS0618
				return CreateDatabaseLegacyAsync(clusterFile, ct);
#pragma warning restore CS0618
			}

			// Starting from 6.1, creating a database handler can be done directly
			var err = FdbNative.CreateDatabase(clusterFile, out var handle);
			FdbNative.DieOnError(err);

			return new ValueTask<IFdbDatabaseHandler>(new FdbNativeDatabase(handle, clusterFile));
		}

		[Obsolete("Deprecated since API level 610")]
		private static async ValueTask<IFdbDatabaseHandler> CreateDatabaseLegacyAsync(string? clusterFile, CancellationToken ct)
		{
			// In legacy API versions, you first had to create "cluster" handle and then obtain a database handle that that cluster.
			// The API is async, but the future always completed inline...

			ClusterHandle? cluster = null;
			DatabaseHandle? database = null;
			try
			{
				cluster = await FdbFuture.CreateTaskFromHandle(
					FdbNative.CreateCluster(clusterFile),
					h =>
					{
						var err = FdbNative.FutureGetCluster(h, out var handle);
						FdbNative.DieOnError(err);

						return handle;
					},
					ct).ConfigureAwait(false);

				database = await FdbFuture.CreateTaskFromHandle(
					FdbNative.ClusterCreateDatabase(cluster, "DB"),
					h =>
					{
						var err = FdbNative.FutureGetDatabase(h, out var handle);
						FdbNative.DieOnError(err);

						return handle;
					}, 
					ct).ConfigureAwait(false);

				return new FdbNativeDatabase(database, clusterFile, cluster);
			}
			catch (Exception)
			{
				database?.Dispose();
				cluster?.Dispose();
				throw;
			}
		}

		public string? ClusterFile => m_clusterFile;

		public bool IsInvalid => m_handle.IsInvalid;

		public bool IsClosed => m_handle.IsClosed;

		public int GetApiVersion() => Fdb.ApiVersion;

		public int GetMaxApiVersion() => FdbNative.GetMaxApiVersion();

		public void SetOption(FdbDatabaseOption option, ReadOnlySpan<byte> data)
		{
			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				fixed (byte* ptr = data)
				{
					FdbNative.DieOnError(FdbNative.DatabaseSetOption(m_handle, option, ptr, data.Length));
				}
			}
		}

		public IFdbTransactionHandler CreateTransaction(FdbOperationContext context)
		{
			TransactionHandle? handle = null;
			try
			{
				var err = FdbNative.DatabaseCreateTransaction(m_handle, out handle);
				FdbNative.DieOnError(err);

				return new FdbNativeTransaction(this, null, handle);
			}
			catch(Exception)
			{
				handle?.Dispose();
				throw;
			}
		}

		public IFdbTenantHandler OpenTenant(FdbTenantName name)
		{
			TenantHandle? handle = null;

			try
			{
				var err = FdbNative.DatabaseOpenTenant(m_handle, name.Value.Span, out handle);
				FdbNative.DieOnError(err);

				return new FdbNativeTenant(this, handle);
			}
			catch (Exception)
			{
				handle?.Dispose();
				throw;
			}
		}

		public double GetMainThreadBusyness()
		{
			return FdbNative.GetMainThreadBusyness(m_handle);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				m_handle.Dispose();
				m_clusterHandle?.Dispose();
			}
		}
	}

}
