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

namespace FoundationDB.Client.Native
{
	using FoundationDB.Async;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Utils;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Wraps a native FDBCluster* handle</summary>
	internal sealed class FdbNativeCluster : FdbFutureContext<ClusterHandle>, IFdbClusterHandler
	{
		//private readonly ClusterHandle m_handle;

		public FdbNativeCluster(ClusterHandle handle)
			: base(handle)
		{
		}

		public static Task<IFdbClusterHandler> CreateClusterAsync(string clusterFile, CancellationToken cancellationToken)
		{
			return FdbNative.GlobalContext.CreateClusterAsync(clusterFile, cancellationToken);
		}

		public bool IsInvalid { get { return m_handle.IsInvalid; } }

		public bool IsClosed { get { return m_handle.IsClosed; } }

		public void SetOption(FdbClusterOption option, Slice data)
		{
			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				if (data.IsNull)
				{
					Fdb.DieOnError(FdbNative.ClusterSetOption(m_handle, option, null, 0));
				}
				else
				{
					fixed (byte* ptr = data.Array)
					{
						Fdb.DieOnError(FdbNative.ClusterSetOption(m_handle, option, ptr + data.Offset, data.Count));
					}
				}
			}
		}

		public Task<IFdbDatabaseHandler> OpenDatabaseAsync(string databaseName, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<IFdbDatabaseHandler>(cancellationToken);

			return RunAsync(
				(handle, state) => FdbNative.ClusterCreateDatabase(handle, state),
				databaseName,
				(h, state) =>
				{
					DatabaseHandle database;
					var err = FdbNative.FutureGetDatabase(h, out database);
					if (err != FdbError.Success)
					{
						database.Dispose();
						throw Fdb.MapToException(err);
					}
					var handler = new FdbNativeDatabase(database, (string)state);
                    return (IFdbDatabaseHandler) handler;
				},
				databaseName,
				cancellationToken
			);
		}

	}


}
