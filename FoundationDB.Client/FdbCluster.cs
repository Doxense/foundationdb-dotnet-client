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
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;
	using JetBrains.Annotations;

	/// <summary>FoundationDB Cluster</summary>
	public class FdbCluster : IFdbCluster
	{

		/// <summary>Underlying handler for this cluster (native, dummy, memory, ...)</summary>
		[NotNull]
		internal IFdbClusterHandler Handler { get; }

		/// <summary>Path to the cluster file used by this connection, or null if the default cluster file is being used</summary>
		public string Path { get; }

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		/// <summary>Wraps a cluster handle</summary>
		public FdbCluster(IFdbClusterHandler handler, string path)
		{
			Contract.NotNull(handler, nameof(handler));

			this.Handler = handler;
			this.Path = path;
		}

		#region IDisposable...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ThrowIfDisposed()
		{
			if (m_disposed) throw ThrowHelper.ObjectDisposedException(this);
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
					if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, nameof(Dispose), "Disposing cluster handler");
					try { this.Handler.Dispose(); }
					catch (Exception e)
					{
						if (Logging.On) Logging.Exception(this, nameof(Dispose), e);
					}
				}
			}
		}

		#endregion

		#region IFdbCluster...

		/// <summary>Opens a database on this cluster, configured to only access a specific subspace of keys</summary>
		/// <param name="databaseName">Name of the database. Must be "DB"</param>
		/// <param name="rootContext">Root key context of all the keys that will be accessed.</param>
		/// <param name="keyEncoding">Default key encoding for the global keyspace</param>
		/// <param name="readOnly">If true, the database will only allow read operations.</param>
		/// <param name="ct">Cancellation Token (optionnal) for the connect operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		/// <remarks>Any attempt to use a key outside the specified subspace will throw an exception</remarks>
		public async Task<IFdbDatabase> OpenDatabaseAsync([NotNull] string databaseName, [NotNull] IKeyContext rootContext, [CanBeNull] IKeyEncoding keyEncoding, bool readOnly, CancellationToken ct)
		{
			Contract.NotNullOrEmpty(databaseName, nameof(databaseName));
			Contract.NotNull(rootContext, nameof(rootContext));
			return await OpenDatabaseInternalAsync(databaseName, rootContext, keyEncoding, readOnly: readOnly, ownsCluster: false, ct: ct).ConfigureAwait(false);
		}

		/// <summary>Opens a database on this cluster</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB'</param>
		/// <param name="rootContext">Root key context of all the keys that will be accessed.</param>
		/// <param name="keyEncoding">Default key encoding for the global keyspace</param>
		/// <param name="readOnly">If true, the database will only allow read operations.</param>
		/// <param name="ownsCluster">If true, the database will dispose this cluster when it is disposed.</param>
		/// <param name="ct">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="databaseName"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		[ItemNotNull]
		internal async Task<FdbDatabase> OpenDatabaseInternalAsync(string databaseName, IKeyContext rootContext, IKeyEncoding keyEncoding, bool readOnly, bool ownsCluster, CancellationToken ct)
		{
			ThrowIfDisposed();
			Contract.NotNullOrEmpty(databaseName, nameof(databaseName));
			Contract.NotNull(rootContext, nameof(rootContext));

			if (Logging.On) Logging.Info(typeof(FdbCluster), nameof(OpenDatabaseInternalAsync), $"Connecting to database '{databaseName}' ...");

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var handler = await this.Handler.OpenDatabaseAsync(databaseName, ct).ConfigureAwait(false);

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(typeof(FdbCluster), nameof(OpenDatabaseInternalAsync), "Connected to database '{databaseName}'");

			return FdbDatabase.Create(this, handler, databaseName, rootContext, keyEncoding, null, readOnly, ownsCluster);
		}

		/// <summary>Set an option on this cluster that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbClusterOption option)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, nameof(SetOption), $"Setting cluster option {option.ToString()}");

			this.Handler.SetOption(option, Slice.Nil);
		}

		/// <summary>Set an option on this cluster that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		public void SetOption(FdbClusterOption option, string value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, nameof(SetOption), $"Setting cluster option {option.ToString()} to '{value ?? "<null>"}'");

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			this.Handler.SetOption(option, data);
		}

		/// <summary>Set an option on this cluster that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		public void SetOption(FdbClusterOption option, long value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, nameof(SetOption), $"Setting cluster option {option.ToString()} to {value}");

			var data = Slice.FromFixed64(value);
			this.Handler.SetOption(option, data);
		}

		#endregion

	}

}
