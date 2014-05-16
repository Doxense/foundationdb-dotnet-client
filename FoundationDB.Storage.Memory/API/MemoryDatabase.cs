#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>In-memory database instance</summary>
	public class MemoryDatabase : FdbDatabase
	{

		#region Static Helpers...

		public static MemoryDatabase CreateNew()
		{
			return CreateNew("DB", FdbSubspace.Empty, false);
		}

		public static MemoryDatabase CreateNew(string name)
		{
			return CreateNew(name, FdbSubspace.Empty, false);
		}

		public static MemoryDatabase CreateNew(string name, FdbSubspace globalSpace, bool readOnly)
		{
			globalSpace = globalSpace ?? FdbSubspace.Empty;
			var uid = Guid.NewGuid();

			MemoryClusterHandler cluster = null;
			MemoryDatabaseHandler db = null;
            try
			{
				cluster = new MemoryClusterHandler();
				db = cluster.OpenDatabase(uid);

				// initialize the system keys for this new db
				db.PopulateSystemKeys();

				return new MemoryDatabase(new FdbCluster(cluster, ":memory:"), db, name, globalSpace, null, readOnly, true);
			}
			catch
			{
				if (db != null) db.Dispose();
				if (cluster != null) cluster.Dispose();
				throw;
			}
		}

		public static async Task<MemoryDatabase> LoadFromAsync(string path, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			MemoryClusterHandler cluster = null;
			MemoryDatabaseHandler db = null;
			try
			{
				cluster = new MemoryClusterHandler();
                db = cluster.OpenDatabase(Guid.Empty);

				// load the snapshot from the disk
				var options = new MemorySnapshotOptions(); //TODO!
                await db.LoadSnapshotAsync(path, options, cancellationToken);

				return new MemoryDatabase(new FdbCluster(cluster, ":memory:"), db, "DB", FdbSubspace.Empty, null, false, true);
			}
			catch(Exception)
			{
				if (db != null) db.Dispose();
				if (cluster != null) cluster.Dispose();
				throw;
			}
		}

		#endregion

		private readonly MemoryDatabaseHandler m_handler;

		private MemoryDatabase(IFdbCluster cluster, MemoryDatabaseHandler handler, string name, FdbSubspace globalSpace, IFdbDirectory directory, bool readOnly, bool ownsCluster)
			: base(cluster, handler, name, globalSpace, directory, readOnly, ownsCluster)
		{
			m_handler = handler;
		}

		[Conditional("DEBUG")]
		public void Debug_Dump(bool detailed = false)
		{
			m_handler.Debug_Dump(detailed);
		}

		/// <summary>Trigger a garbage collection of the memory database</summary>
		/// <remarks>If the amount of memory that can be collected is too small, this operation will do nothing.</remarks>
		public void Collect()
		{
			m_handler.Collect();
		}

		/// <summary>Replace the content of the database with existing data.</summary>
		/// <param name="data">Data that will replace the content of the database. The elements do not need to be sorted, but best performance is achieved if all the keys are lexicographically ordered (smallest to largest)</param>
		/// <param name="cancellationToken">Optionnal cancellation token</param>
		/// <returns>Task that completes then the data has been loaded into the database</returns>
		/// <remarks>Any pre-existing data will be removed!</remarks>
		public Task BulkLoadAsync(IEnumerable<KeyValuePair<Slice, Slice>> data, bool ordered = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (data == null) throw new ArgumentNullException("data");
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(cancellationToken);

			var coll = data as ICollection<KeyValuePair<Slice, Slice>> ?? data.ToList();

			return m_handler.BulkLoadAsync(coll, ordered, false, cancellationToken);
		}

		public Task SaveSnapshotAsync(string path, MemorySnapshotOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (path == null) throw new ArgumentNullException("path");
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(cancellationToken);

			options = options ?? new MemorySnapshotOptions()
			{
				Mode = MemorySnapshotMode.Full
			};

			return m_handler.SaveSnapshotAsync(path, options, cancellationToken);
		}

	}

}
