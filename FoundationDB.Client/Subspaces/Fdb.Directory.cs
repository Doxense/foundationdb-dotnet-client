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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Linq;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using SystemIO = System.IO;

	public static partial class Fdb
	{

		/// <summary>Static helper class to open named partitions</summary>
		public static class Directory
		{
			/// <summary>Open a named partition of the default cluster</summary>
			/// <param name="path">Path of the named partition to open</param>
			/// <param name="cancellationToken">Token used to cancel this operation</param>
			/// <returns>Returns a new database instance that will only be able to read and write inside the specified partition. If the partition does not exist, it will be automatically created</returns>
			public static Task<IFdbDatabase> OpenNamedPartitionAsync([NotNull] IEnumerable<string> path, CancellationToken cancellationToken)
			{
				return OpenNamedPartitionAsync(clusterFile: null, dbName: null, path: path, readOnly: false, cancellationToken: cancellationToken);
			}

			/// <summary>Open a named partition of a specific cluster</summary>
			/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
			/// <param name="dbName">Name of the database, or "DB" if not specified.</param>
			/// <param name="path">Path of the named partition to open</param>
			/// <param name="readOnly">If true, the database instance will only allow read operations</param>
			/// <param name="cancellationToken">Token used to cancel this operation</param>
			/// <returns>Returns a new database instance that will only be able to read and write inside the specified partition. If the partition does not exist, it will be automatically created</returns>
			public static async Task<IFdbDatabase> OpenNamedPartitionAsync(string clusterFile, string dbName, [NotNull] IEnumerable<string> path, bool readOnly, CancellationToken cancellationToken)
			{
				if (path == null) throw new ArgumentNullException("path");
				var partitionPath = path.ToList();
				if (partitionPath.Count == 0) throw new ArgumentException("The path to the named partition cannot be empty", "path");

				// looks at the global partition table for the specified named partition

				// By convention, all named databases will be under the "/Databases" folder
				FdbDatabase db = null;
				FdbSubspace rootSpace = FdbSubspace.Empty;
				try
				{
					db = await Fdb.OpenInternalAsync(clusterFile, dbName, rootSpace, readOnly: false, cancellationToken: cancellationToken).ConfigureAwait(false);
					var rootLayer = FdbDirectoryLayer.Create(rootSpace);
					if (Logging.On) Logging.Verbose(typeof(Fdb.Directory), "OpenNamedPartitionAsync", String.Format("Opened root layer of database {0} using cluster file '{1}'", db.Name, db.Cluster.Path));

					// look up in the root layer for the named partition
					var descriptor = await rootLayer.CreateOrOpenAsync(db, partitionPath, layer: FdbDirectoryPartition.LayerId, cancellationToken: cancellationToken).ConfigureAwait(false);
					if (Logging.On) Logging.Verbose(typeof(Fdb.Directory), "OpenNamedPartitionAsync", String.Format("Found named partition '{0}' at prefix {1}", descriptor.FullName, descriptor));

					// we have to chroot the database to the new prefix, and create a new DirectoryLayer with a new '/'
					rootSpace = FdbSubspace.Copy(descriptor); //note: create a copy of the key
					//TODO: find a nicer way to do that!
					db.ChangeRoot(rootSpace, FdbDirectoryLayer.Create(rootSpace, partitionPath), readOnly);

					if (Logging.On) Logging.Info(typeof(Fdb.Directory), "OpenNamedPartitionAsync", String.Format("Opened partition {0} at {1}, using directory layer at {2}", descriptor.FullName, db.GlobalSpace, db.Directory.DirectoryLayer.NodeSubspace));

					return db;
				}
				catch(Exception e)
				{
					if (db != null) db.Dispose();
					if (Logging.On) Logging.Exception(typeof(Fdb.Directory), "OpenNamedPartitionAsync", e);
					throw;
				}
			}

			/// <summary>List and open the sub-directories of the given directory</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="parent">Parent directory</param>
			/// <param name="cancellationToken">Token used to cancel this operation</param>
			/// <returns>Dictionary of all the sub directories of the <paramref name="parent"/> directory.</returns>
			public static async Task<Dictionary<string, FdbDirectorySubspace>> BrowseAsync([NotNull] IFdbDatabase db, [NotNull] IFdbDirectory parent, CancellationToken cancellationToken)
			{
				if (db == null) throw new ArgumentNullException("db");
				if (parent == null) throw new ArgumentNullException("parent");

				return await db.ReadAsync(async (tr) =>
				{
					// read the names of all the subdirectories
					var names = await parent.ListAsync(tr).ConfigureAwait(false);

					// open all the subdirectories
					var folders = await names
						.ToAsyncEnumerable()
						.SelectAsync((name, ct) => parent.OpenAsync(tr, name))
						.ToListAsync();

					// map the result
					return folders.ToDictionary(ds => ds.Name);

				}, cancellationToken).ConfigureAwait(false);
			}
		}

	}

}
