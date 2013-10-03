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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>FoundationDB Cluster</summary>
	/// <remarks>Wraps an FDBCluster* handle</remarks>
	public sealed class FdbCluster : IDisposable
	{

		private readonly ClusterHandle m_handle;
		private readonly string m_path;
		private bool m_disposed;

		internal FdbCluster(ClusterHandle handle, string path)
		{
			m_handle = handle;
			m_path = path;
		}

		internal ClusterHandle Handle { get { return m_handle; } }

		/// <summary>Path to the cluster file used by this connection, or null if the default cluster file is being used</summary>
		public string Path { get { return m_path; } }

		private void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(null);
		}

		/// <summary>Close the connection with the FoundationDB cluster</summary>
		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				m_handle.Dispose();
			}
		}

		/// <summary>Opens a database on this cluster</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB'</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="databaseName"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>As of Beta2, the only supported database name is 'DB'</remarks>
		public Task<FdbDatabase> OpenDatabaseAsync(string databaseName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return OpenDatabaseAsync(databaseName, FdbSubspace.Empty, readOnly: false, ownsCluster: false, cancellationToken: cancellationToken);
		}

		/// <summary>Opens a database on this cluster, configured to only access a specific subspace of keys</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB' (as of Beta 2)</param>
		/// <param name="subspace">Subspace of keys that will be accessed.</param>
		/// <param name="cancellationToken">Cancellation Token (optionnal) for the connect operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="databaseName"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>Any attempt to use a key outside the specified subspace will throw an exception</remarks>
		public Task<FdbDatabase> OpenDatabaseAsync(string databaseName, FdbSubspace subspace, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			return OpenDatabaseAsync(databaseName, subspace, readOnly: false, ownsCluster: false, cancellationToken: cancellationToken);
		}

		/// <summary>Opens a database on this cluster</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB'</param>
		/// <param name="subspace">Subspace of keys that will be accessed.</param>
		/// <param name="ownsCluster">If true, the database will dispose this cluster when it is disposed.</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="databaseName"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>As of Beta2, the only supported database name is 'DB'</remarks>
		internal Task<FdbDatabase> OpenDatabaseAsync(string databaseName, FdbSubspace subspace, bool readOnly, bool ownsCluster, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			if (string.IsNullOrEmpty(databaseName)) throw new ArgumentNullException("databaseName");
			if (subspace == null) throw new ArgumentNullException("rootNamespace");

			if (Logging.On) Logging.Info(typeof(FdbCluster), "OpenDatabaseAsync", String.Format("Connecting to database '{0}' ...", databaseName));

			if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

			var future = FdbNative.ClusterCreateDatabase(m_handle, databaseName);

			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					DatabaseHandle database;
					var err = FdbNative.FutureGetDatabase(h, out database);
					if (err != FdbError.Success)
					{
						database.Dispose();
						throw Fdb.MapToException(err);
					}

					if (Logging.On && Logging.IsVerbose) Logging.Verbose(typeof(FdbCluster), "OpenDatabaseAsync", String.Format("Connected to database '{0}'", databaseName));

					return new FdbDatabase(this, database, databaseName, subspace, readOnly, ownsCluster);
				},
				cancellationToken
			);
		}

		/// <summary>Set an option on this cluster that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbClusterOption option)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				Fdb.DieOnError(FdbNative.ClusterSetOption(m_handle, option, null, 0));
			}
		}

		/// <summary>Set an option on this cluster that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		public void SetOption(FdbClusterOption option, string value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			unsafe
			{
				fixed (byte* ptr = data.Array)
				{
					Fdb.DieOnError(FdbNative.ClusterSetOption(m_handle, option, ptr + data.Offset, data.Count));
				}
			}
		}


	}

}
