#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.IO
{
	using FoundationDB.Client;
	using System;

	internal static class SnapshotFormat
	{

		[Flags]
		public enum Flags : ulong
		{ 
			None = 0,

			TYPE_SNAPSHOT_VERSIONNED = 0,
			TYPE_SNAPSHOT_COMPACT = 1,

			COMPRESSED = 0x100,
			SIGNED = 0x200,
			ENCRYPTED = 0x400,
		}

		// Size of the in-memory buffer while writing a snapshot (optimized for SSD?)
		public const int FLUSH_SIZE_BITS = 20; // 1MB
		public const int FLUSH_SIZE = 1 << FLUSH_SIZE_BITS;

		// For convenience, some variable-size sections (header, ...) will be padded to a 'page' size.
		// => note: the Jump Table must fit in a single page so could probably not be smaller than 512 ...
		public const int PAGE_SIZE_BITS = 10; // 1KB
		public const int PAGE_SIZE = 1 << PAGE_SIZE_BITS;

		public const uint DB_HEADER_MAGIC_NUMBER = 0x42444E50; // "PNDB"
		public const uint JUMP_TABLE_MAGIC_NUMBER = 0x54504D4A; // "JMPT"
		// Size of the header CRC (in bytes)
		public const int DB_INFO_BYTES = 64;
		public const int HEADER_CRC_SIZE = 4;

		public static uint ComputeChecksum(Slice data)
		{
			//BUGBUG: use a REAL hashcode !
			return (uint)data.GetHashCode();
		}

	}

}
