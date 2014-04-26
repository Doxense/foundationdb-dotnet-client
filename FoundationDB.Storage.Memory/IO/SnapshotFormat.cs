#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.IO
{
	using FoundationDB.Client;
	using FoundationDB.Storage.Memory.Utils;
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

		public const uint HEADER_MAGIC_NUMBER = 0x42444E50; // "PNDB"
		public const uint JUMP_TABLE_MAGIC_NUMBER = 0x54504D4A; // "JMPT"
		public const uint LEVEL_MAGIC_NUMBER = 0x204C564C; // "LVL ";

		// Size of the header CRC (in bytes)
		public const int HEADER_METADATA_BYTES = 64;
		public const int HEADER_CRC_SIZE = 4;
		public const int LEVEL_HEADER_BYTES = 16;

		// The maximum size for key + value is 10,000 + 100,000 with 2 + 3 additional bytes to encode the variable-length size
		// The buffer size should be multiple of the pageSize value AND a power of two for convenience.
		// Also, it would help if the buffer is x2 that to simplify buffering
		// The worst case scenario would be where the first byte of the key starts on the last byte of a page, and last byte of the value cross into a new page, added 2 pages to the total
		// Minimum size will be 2 + 10,000 + 3 + 100,000 + 2 * 1,024 = 112,053 and the next power of two is 2 ^ 17, so use 2 ^ 18 for double buffering
		public const int MAX_KEYVALUE_BITS = 18;
		public const int BUFFER_SIZE = 1 << MAX_KEYVALUE_BITS;

		public static uint ComputeChecksum(Slice data)
		{
			if (data.Offset < 0 || data.Count < 0 || (data.Array == null && data.Count > 0)) throw new ArgumentException("Data is invalid");

			unsafe
			{
				fixed (byte* ptr = data.Array)
				{
					return ComputeChecksum(ptr + data.Offset, (ulong)data.Count);
				}
			}
		}

		public static unsafe uint ComputeChecksum(byte* start, ulong count)
		{
			if (start == null && count != 0) throw new ArgumentException("Invalid address");

			byte* ptr = start;
			byte* end = checked(ptr + count);

			// <HACKHACK>: unoptimized 32 bits FNV-1a implementation
			uint h = 2166136261; // FNV1 32 bits offset basis
			while (ptr < end)
			{
				h = (h ^ *ptr) * 16777619;   // FNV1 32 prime
				++ptr;
			}
			return h;
			// </HACKHACK>
		}

	}

}
