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
using System.Threading.Tasks;
using System.Threading;

namespace FoundationDb.Client
{

	/// <summary>FoundationDB Cluster</summary>
	/// <remarks>Wraps an FDBCluster* handle</remarks>
	public class FdbCluster : IDisposable
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

		public string Path { get { return m_path; } }

		private void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(null);
		}

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
		/// <param name="ct">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="name"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		/// <remarks>As of Beta1, the only supported database name is 'DB'</remarks>
		public Task<FdbDatabase> OpenDatabaseAsync(string databaseName, CancellationToken ct = default(CancellationToken))
		{
			return OpenDatabaseAsync(databaseName, false, ct);
		}

		/// <summary>Opens a database on this cluster</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB'</param>
		/// <param name="ownsCluster">If true, the database will dispose this cluster when it is disposed.</param>
		/// <param name="ct">Cancellation Token</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="System.InvalidOperationException">If <paramref name="name"/> is anything other than 'DB'</exception>
		/// <exception cref="System.OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		/// <remarks>As of Beta1, the only supported database name is 'DB'</remarks>
		internal Task<FdbDatabase> OpenDatabaseAsync(string databaseName, bool ownsCluster, CancellationToken ct)
		{
			ThrowIfDisposed();
			if (string.IsNullOrEmpty(databaseName)) throw new ArgumentNullException("databaseName");

			// BUGBUG: the only accepted name is "DB".
			// Currently in Beta1, if you create a database with any other name, it will succeed but any transaction performed on it will fail (future will never complete)
			// Hope that it will be changed in the future to return an error code if the name is not valid.
			if (databaseName != "DB") throw new InvalidOperationException("This version of FoundationDB only allows connections to the 'DB' database");

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
					Debug.WriteLine("FutureGetDatabase => 0x" + database.Handle.ToString("x"));

					return new FdbDatabase(this, database, databaseName, ownsCluster);
				},
				ct
			);
		}

	}

}
