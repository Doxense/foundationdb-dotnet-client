#region BSD License
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
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq;
	using FoundationDB.Layers.Directories;
	using JetBrains.Annotations;

	public static partial class Fdb
	{

		/// <summary>Static helper class to open named partitions</summary>
		[PublicAPI]
		public static class Directory
		{
			/// <summary>Open a named partition of the default cluster</summary>
			/// <param name="path">Path of the named partition to open</param>
			/// <param name="ct">Token used to cancel this operation</param>
			/// <returns>Returns a new database instance that will only be able to read and write inside the specified partition. If the partition does not exist, it will be automatically created</returns>
			[Obsolete("Use " + nameof(Fdb.OpenAsync) + "(" + nameof(FdbConnectionOptions) + ", ...) instead")]
			[ItemNotNull]
			public static Task<IFdbDatabase> OpenNamedPartitionAsync([NotNull] IEnumerable<string> path, CancellationToken ct)
			{
				return OpenNamedPartitionAsync(clusterFile: null, dbName: null, path: path, readOnly: false, ct: ct);
			}

			/// <summary>Open a named partition of a specific cluster</summary>
			/// <param name="clusterFile">Path to the 'fdb.cluster' file to use, or null for the default cluster file</param>
			/// <param name="dbName">Name of the database, or "DB" if not specified.</param>
			/// <param name="path">Path of the named partition to open</param>
			/// <param name="readOnly">If true, the database instance will only allow read operations</param>
			/// <param name="ct">Token used to cancel this operation</param>
			/// <returns>Returns a new database instance that will only be able to read and write inside the specified partition. If the partition does not exist, it will be automatically created</returns>
			[Obsolete("Use " + nameof(Fdb.OpenAsync) + "(" + nameof(FdbConnectionOptions) + ", ...) instead")]
			[ItemNotNull]
			public static async Task<IFdbDatabase> OpenNamedPartitionAsync(string clusterFile, string dbName, [NotNull] IEnumerable<string> path, bool readOnly, CancellationToken ct)
			{
				Contract.NotNull(path, nameof(path));
				var partitionPath = (path as string[]) ?? path.ToArray();
				if (partitionPath.Length == 0) throw new ArgumentException("The path to the named partition cannot be empty", nameof(path));

				var options = new FdbConnectionOptions
				{
					ClusterFile = clusterFile,
					DbName = dbName,
					PartitionPath = partitionPath,
				};
				var db = await Fdb.OpenInternalAsync(options, ct).ConfigureAwait(false);
				return db;
			}

			/// <summary>Opens a named partition, and change the root subspace of the database to the corresponding prefix</summary>
			internal static async Task SwitchToNamedPartitionAsync([NotNull] FdbDatabase db, [NotNull, ItemNotNull] string[] path, bool readOnly, CancellationToken ct)
			{
				Contract.Requires(db != null && path != null);
				ct.ThrowIfCancellationRequested();

				if (path.Length == 0) throw new ArgumentException("The path to the named partition cannot be empty", nameof(path));

				if (Logging.On) Logging.Verbose(typeof(Fdb.Directory), "OpenNamedPartitionAsync", $"Opened root layer of database {db.Name} using cluster file '{db.Cluster.Path}'");

				// look up in the root layer for the named partition
				var descriptor = await db.ReadWriteAsync(tr => db.Directory.CreateOrOpenAsync(tr, path, layer: FdbDirectoryPartition.LayerId), ct).ConfigureAwait(false);
				if (Logging.On) Logging.Verbose(typeof(Fdb.Directory), "OpenNamedPartitionAsync", $"Found named partition '{descriptor.FullName}' at prefix {descriptor}");

				// we have to chroot the database to the new prefix, and create a new DirectoryLayer with a new '/'
				var rootSpace = descriptor.Copy(); //note: create a copy of the key
				//TODO: find a nicer way to do that!
				db.ChangeRoot(rootSpace, FdbDirectoryLayer.Create(rootSpace, path), readOnly);

				if (Logging.On) Logging.Info(typeof(Fdb.Directory), "OpenNamedPartitionAsync", $"Opened partition {descriptor.FullName} at {db.GlobalSpace}, using directory layer at {db.Directory.DirectoryLayer.NodeSubspace}");
			}

			/// <summary>List and open the sub-directories of the given directory</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="parent">Parent directory</param>
			/// <param name="ct">Token used to cancel this operation</param>
			/// <returns>Dictionary of all the sub directories of the <paramref name="parent"/> directory.</returns>
			[ItemNotNull]
			public static async Task<Dictionary<string, FdbDirectorySubspace>> BrowseAsync([NotNull] IFdbDatabase db, [NotNull] IFdbDirectory parent, CancellationToken ct)
			{
				Contract.NotNull(db, nameof(db));
				Contract.NotNull(parent, nameof(parent));

				return await db.ReadAsync(async (tr) =>
				{
					// read the names of all the subdirectories
					var names = await parent.ListAsync(tr).ConfigureAwait(false);

					// open all the subdirectories
					var folders = await names
						.ToAsyncEnumerable()
						.SelectAsync((name, _) => parent.OpenAsync(tr, name))
						.ToListAsync(ct);

					// map the result
					return folders.ToDictionary(ds => ds.Name);

				}, ct).ConfigureAwait(false);
			}
		}

	}

}
