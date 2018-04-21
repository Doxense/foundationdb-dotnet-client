#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>FoundationDB Cluster</summary>
	public class FdbCluster : IFdbCluster
	{

		/// <summary>Underlying handler for this cluster (native, dummy, memory, ...)</summary>
		private readonly IFdbClusterHandler m_handler;

		/// <summary>Path to the cluster file userd by this connection</summary>
		private readonly string m_path;

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		/// <summary>Wraps a cluster handle</summary>
		public FdbCluster(IFdbClusterHandler handler, string path)
		{
			if (handler == null) throw new ArgumentNullException("handler");

			m_handler = handler;
			m_path = path;
		}

		/// <summary>Path to the cluster file used by this connection, or null if the default cluster file is being used</summary>
		public string Path
		{
			get { return m_path; }
		}

		internal IFdbClusterHandler Handler
		{
			[NotNull]
			get { return m_handler; }
		}

		private void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(null);
		}

		/// <summary>Close the connection with the FoundationDB cluster</summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (disposing)
				{
					if (m_handler != null)
					{
						if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Dispose", "Disposing cluster handler");
						try { m_handler.Dispose(); }
						catch (Exception e)
						{
							if (Logging.On) Logging.Exception(this, "Dispose", e);
						}
					}
				}
			}
		}

		/// <summary>Opens a database on this cluster, configured to only access a specific subspace of keys</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB' (as of Beta 2)</param>
		/// <param name="subspace">Subspace of keys that will be accessed.</param>
		/// <param name="readOnly">If true, the database will only allow read operations.</param>
		/// <param name="cancellationToken">Cancellation Token (optionnal) for the connect operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="databaseName"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>Any attempt to use a key outside the specified subspace will throw an exception</remarks>
		[ItemNotNull]
		public async Task<IFdbDatabase> OpenDatabaseAsync(string databaseName, IKeySubspace subspace, bool readOnly, CancellationToken cancellationToken)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			return await OpenDatabaseInternalAsync(databaseName, subspace, readOnly: readOnly, ownsCluster: false, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Opens a database on this cluster</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB'</param>
		/// <param name="subspace">Subspace of keys that will be accessed.</param>
		/// <param name="readOnly">If true, the database will only allow read operations.</param>
		/// <param name="ownsCluster">If true, the database will dispose this cluster when it is disposed.</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="databaseName"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="cancellationToken"/> is cancelled</exception>
		/// <remarks>As of Beta2, the only supported database name is 'DB'</remarks>
		[ItemNotNull]
		internal async Task<FdbDatabase> OpenDatabaseInternalAsync(string databaseName, IKeySubspace subspace, bool readOnly, bool ownsCluster, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			if (string.IsNullOrEmpty(databaseName)) throw new ArgumentNullException("databaseName");
			if (subspace == null) throw new ArgumentNullException("subspace");

			if (Logging.On) Logging.Info(typeof(FdbCluster), "OpenDatabaseAsync", String.Format("Connecting to database '{0}' ...", databaseName));

			if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

			var handler = await m_handler.OpenDatabaseAsync(databaseName, cancellationToken).ConfigureAwait(false);

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(typeof(FdbCluster), "OpenDatabaseAsync", String.Format("Connected to database '{0}'", databaseName));

			return FdbDatabase.Create(this, handler, databaseName, subspace, null, readOnly, ownsCluster);
		}

		/// <summary>Set an option on this cluster that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbClusterOption option)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", String.Format("Setting cluster option {0}", option.ToString()));

			m_handler.SetOption(option, Slice.Nil);
		}

		/// <summary>Set an option on this cluster that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		public void SetOption(FdbClusterOption option, string value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", String.Format("Setting cluster option {0} to '{1}'", option.ToString(), value ?? "<null>"));

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			m_handler.SetOption(option, data);
		}

		/// <summary>Set an option on this cluster that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		public void SetOption(FdbClusterOption option, long value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", String.Format("Setting cluster option {0} to {1}", option.ToString(), value));

			var data = Slice.FromFixed64(value);
			m_handler.SetOption(option, data);
		}

	}

}
