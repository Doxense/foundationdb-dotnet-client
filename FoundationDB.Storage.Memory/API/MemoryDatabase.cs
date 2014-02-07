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

	public class MemoryDatabase : FdbDatabase
	{

		public static MemoryDatabase CreateNew(string name)
		{
			return CreateNew(name, FdbSubspace.Empty, false);
		}

		public static MemoryDatabase CreateNew(string name, FdbSubspace globalSpace, bool readOnly)
		{
			globalSpace = globalSpace ?? FdbSubspace.Empty;

			var cluster = new FdbCluster(new MemoryClusterHandler(), ":memory:");

			var uid = Guid.NewGuid();

			return new MemoryDatabase(cluster, new MemoryDatabaseHandler(uid), name, globalSpace, null, readOnly, true);
		}

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

			var coll = data as ICollection<KeyValuePair<Slice, Slice>>;
			if (coll == null)
			{
				coll = data.ToList();
			}

			return m_handler.BulkLoadAsync(coll, ordered, cancellationToken);
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


	public enum MemorySnapshotMode
	{
		/// <summary>Include all keys (included the deletions), as well as all their mutations, timestamped with their sequence number</summary>
		Full = 0,
		/// <summary>Include all keys (inlcuded the deletions), but with only their latest value.</summary>
		Last,
		/// <summary>Include only the live keys, with their latest value.</summary>
		Compact,

	}
	public sealed class MemorySnapshotOptions
	{

		public MemorySnapshotOptions()
		{ }

		public MemorySnapshotMode Mode { get; set; }

		public bool Compressed { get; set; }

		public bool Signed { get; set; }

		public bool Encrypted { get; set; }

	}

}
