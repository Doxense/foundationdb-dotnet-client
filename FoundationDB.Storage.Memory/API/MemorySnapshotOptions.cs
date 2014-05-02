#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using System;

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
