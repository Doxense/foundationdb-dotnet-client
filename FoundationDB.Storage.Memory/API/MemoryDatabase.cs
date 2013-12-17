#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using System;
	using System.Diagnostics;

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

			return new MemoryDatabase(cluster, new MemoryDatabaseHandler(), name, globalSpace, readOnly, true);
		}

		private readonly MemoryDatabaseHandler m_handler;

		internal MemoryDatabase(IFdbCluster cluster, MemoryDatabaseHandler handler, string name, FdbSubspace globalSpace, bool readOnly, bool ownsCluster)
			: base(cluster, handler, name, globalSpace, readOnly, ownsCluster)
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

	}

}
