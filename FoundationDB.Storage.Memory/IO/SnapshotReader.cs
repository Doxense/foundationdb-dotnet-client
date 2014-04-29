#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.IO
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Storage.Memory.API;
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Threading;
	using System.Threading.Tasks;

	internal unsafe sealed class SnapshotReader
	{
		private struct LevelAddress
		{
			public ulong Offset;
			public ulong Size;
			public ulong PaddedSize;
		}

		private Win32MemoryMappedFile m_file;

		private bool m_hasHeader;
		private bool m_hasJumpTable;
		private LevelAddress[] m_jumpTable;

		private Version m_version;
		private SnapshotFormat.Flags m_dbFlags;
		private Uuid m_uid;
		private ulong m_sequence;
		private long m_itemCount;
		private ulong m_timestamp;
		private uint m_headerChecksum;
		private Dictionary<string, IFdbTuple> m_attributes;

		private uint m_pageSize;
		private uint m_headerSize;

		private ulong m_dataStart;
		private ulong m_dataEnd;

		private int m_levels;

		public SnapshotReader(Win32MemoryMappedFile file)
		{
			Contract.Requires(file != null); //TODO: && file.CanRead ?
			m_file = file;
		}

		public int Depth
		{
			get { return m_levels; }
		}

		public ulong Sequence { get { return m_sequence; } }
		public ulong TimeStamp { get { return m_timestamp; } }
		public Version Version { get { return m_version; } }
		public Uuid Id { get { return m_uid; } }

		private Exception ParseError(string message)
		{
			message = "Database snapshot is invalid or corrupted: " + message;
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			return new InvalidOperationException(message);
		}

		private Exception ParseError(string message, params object[] args)
		{
			return ParseError(String.Format(message, args));
		}

		private static uint RoundDown(uint size, uint pageSize)
		{
			return size & ~(pageSize - 1U);
		}

		private static uint RoundUp(uint size, uint pageSize)
		{
			return checked(size + pageSize - 1U) & ~(pageSize - 1U);
		}

		private static ulong RoundDown(ulong size, uint pageSize)
		{
			return size & ~((ulong)pageSize - 1UL);
		}

		private static ulong RoundUp(ulong size, uint pageSize)
		{
			return checked(size + pageSize - 1UL) & ~((ulong)pageSize - 1UL);
		}

		public void ReadHeader(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			// minimum header prolog size is 64 but most will only a single page
			// we can preallocate a full page, and we will resize it later if needed

			var reader = m_file.CreateReader(0, SnapshotFormat.HEADER_METADATA_BYTES);

			// "PNDB"
			var signature = reader.ReadFixed32();
			// v1.0
			uint major = reader.ReadFixed16();
			uint minor = reader.ReadFixed16();
			m_version = new Version((int)major, (int)minor);
			// FLAGS
			m_dbFlags = (SnapshotFormat.Flags) reader.ReadFixed64();
			// Database ID
			m_uid = new Uuid(reader.ReadBytes(16).GetBytes());
			// Database Version
			m_sequence = reader.ReadFixed64();
			// Number of items in the database
			m_itemCount = checked((long)reader.ReadFixed64());
			// Database Timestamp
			m_timestamp = reader.ReadFixed64();
			// Page Size
			m_pageSize = reader.ReadFixed32();
			// Header Size
			m_headerSize = reader.ReadFixed32();

			Contract.Assert(!reader.HasMore);

			#region Sanity checks

			// Signature
			if (signature != SnapshotFormat.HEADER_MAGIC_NUMBER) throw ParseError("Invalid magic number");

			// Version
			if (m_version.Major != 1) throw ParseError("Unsupported file version (major)");
			if (m_version.Minor > 0) throw ParseError("Unsupported file version (minor)");

			// Flags

			// Page Size
			if (m_pageSize != UnmanagedHelpers.NextPowerOfTwo(m_pageSize)) throw ParseError("Page size ({0}) is not a power of two", m_pageSize);
			if (m_pageSize < SnapshotFormat.HEADER_METADATA_BYTES) throw ParseError("Page size ({0}) is too small", m_pageSize);
			if (m_pageSize > 1 << 20) throw ParseError("Page size ({0}) is too big", m_pageSize);

			// Header Size
			if (m_headerSize < 64 + 4 + 4) throw ParseError("Header size ({0}) is too small", m_headerSize);
			if (m_headerSize > m_file.Length) throw ParseError("Header size is bigger than the file itself ({0} < {1})", m_headerSize, m_file.Length);
			if (m_headerSize > 1 << 10) throw ParseError("Header size ({0}) exceeds the maximum allowed size", m_headerSize);

			#endregion

			// we know the page size and header size, read the rest...

			// read the rest
			reader = m_file.CreateReader(0, m_headerSize);
			reader.Skip(SnapshotFormat.HEADER_METADATA_BYTES);

			// parse the attributes
			Contract.Assert(reader.Offset == SnapshotFormat.HEADER_METADATA_BYTES);
			var attributeCount = checked((int)reader.ReadFixed32());
			if (attributeCount < 0 || attributeCount > 1024) throw ParseError("Attributes count is invalid");

			var attributes = new Dictionary<string, IFdbTuple>(attributeCount);
			for (int i = 0; i < attributeCount; i++)
			{
				var name = reader.ReadVarbytes().ToSlice(); //TODO: max size ?
				if (name.IsNullOrEmpty) throw ParseError("Header attribute name is empty");

				var data = reader.ReadVarbytes().ToSlice();	//TODO: max size + have a small scratch pad buffer for these ?
				var value = FdbTuple.Unpack(data);
				attributes.Add(name.ToUnicode(), value);
			}
			m_attributes = attributes;

			// read the header en marker
			var marker = reader.ReadFixed32();
			if (marker != uint.MaxValue) throw ParseError("Header end marker is invalid");

			// verify the header checksum
			uint actualHeaderChecksum = SnapshotFormat.ComputeChecksum(reader.Base, reader.Offset);
			uint headerChecksum = reader.ReadFixed32();
			m_headerChecksum = headerChecksum;

			if (headerChecksum != actualHeaderChecksum)
			{
				throw ParseError("The header checksum does not match ({0} != {1}). This may be an indication of data corruption", headerChecksum, actualHeaderChecksum);
			}

			m_dataStart = RoundUp(m_headerSize, m_pageSize);
			m_hasHeader = true;
		}

		public bool HasLevel(int level)
		{
			return m_hasJumpTable && level >= 0 && level < m_jumpTable.Length && m_jumpTable[level].Size != 0;
		}

		public void ReadJumpTable(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			if (!m_hasHeader)
			{
				throw new InvalidOperationException("Cannot read the Jump Table without reading the Header first!");
			}

			// an empty database will have at least 2 pages: the header and the JT
			if (m_file.Length < checked(m_pageSize << 1))
			{
				throw ParseError("File size ({0}) is too small to be a valid snapshot", m_file.Length);
			}

			// the jumptable is always in the last page of the file and is expected to fit nicely
			// > file size MUST be evenly divible by page size
			// > then JT offset will be file.Length - pageSize
			if (m_file.Length % m_pageSize != 0)
			{
				throw ParseError("The file size ({0}) is not a multiple of the page size ({1}), which may be a symptom of truncation", m_file.Length, m_pageSize);
			}

			var jumpTableStart = m_file.Length - m_pageSize;
			Contract.Assert(jumpTableStart % m_pageSize == 0);
			m_dataEnd = jumpTableStart;

			var reader = m_file.CreateReader(jumpTableStart, m_pageSize);

			// "JMPT"
			var signature = reader.ReadFixed32();
			// Page Size (repeated)
			var pageSizeRepeated = (int)reader.ReadFixed32();
			// Sequence Number (repeated)
			var sequenceRepeated = reader.ReadFixed64();
			// Database ID (repeated)
			var uidRepeated = new Uuid(reader.ReadBytes(16).GetBytes());
			// Header CRC (repeated)
			var headerChecksumRepeated = reader.ReadFixed32();

			// Sanity checks

			if (signature != SnapshotFormat.JUMP_TABLE_MAGIC_NUMBER) throw ParseError("Last page does not appear to be the Jump Table");
			if (pageSizeRepeated != m_pageSize) throw ParseError("Page size in Jump Table does not match the header value");
			if (sequenceRepeated != m_sequence) throw ParseError("Sequence in Jump Table does not match the header value");
			if (uidRepeated != m_uid) throw ParseError("Database ID in Jump Table does not match the header value");
			if (headerChecksumRepeated != m_headerChecksum) throw ParseError("Database ID in Jump Table does not match the header value");

			// read the table itself
			int levels = (int)reader.ReadFixed32();
			if (levels < 0 || levels > 32) throw ParseError("The number of levels in the snapshot does not appear to be valid");

			var table = new LevelAddress[levels];
			for (int level = 0; level < levels; level++)
			{
				ulong offset = reader.ReadFixed64();
				ulong size = reader.ReadFixed64();

				// Offset and Size cannot be negative
				// Empty levels (size == 0) must have a zero offset
				// Non empty levels (size > 0) must have a non zero offset that is greater than the headerSize
				if ((size == 0 && offset != 0) || (size > 0 && offset < m_dataStart)) throw ParseError("Level in Jump Table has invalid size ({0}) or offset ({1})", size, offset);
				if (checked(offset + size) > m_dataEnd) throw ParseError("Level in Jump Table would end after the end of the file");

				table[level].Offset = offset;
				table[level].Size = size;
				table[level].PaddedSize = RoundUp(size, m_pageSize);
			}

			// end attributes
			uint attributeCount = reader.ReadFixed32();
			if (attributeCount != 0) throw new NotImplementedException("Footer attributes not yet implemented!");

			// end marker
			if (reader.ReadFixed32() != uint.MaxValue) throw ParseError("Jump Table end marker not found");

			// checksum
			uint actualChecksum = SnapshotFormat.ComputeChecksum(reader.Base, reader.Offset);
			uint checksum = reader.ReadFixed32();
			if (actualChecksum != checksum) throw ParseError("Jump Table checksum does not match ({0} != {1}). This may be an indication of data corruption", checksum, actualChecksum);

			m_jumpTable = table;
			m_levels = levels;
			m_hasJumpTable = true;
		}

		public void ReadLevel(int level, LevelWriter writer, CancellationToken ct)
		{
			Contract.Requires(level >= 0 && writer != null);
			ct.ThrowIfCancellationRequested();

			if (!m_hasJumpTable)
			{
				throw new InvalidOperationException("Cannot read a level without reading the Jump Table first!");
			}

			int itemCount = checked(1 << level);

			var address = m_jumpTable[level];

			if (address.Offset < m_dataStart || address.Offset > m_dataEnd)
			{
				throw ParseError("Level {0} offset ({1}) is invalid", level, address.Offset);
			}
			if (checked(address.Offset + address.PaddedSize) > m_dataEnd)
			{
				throw ParseError("Level {0} size ({1}) is invalid", level, address.PaddedSize);
			}

			var reader = m_file.CreateReader(address.Offset, address.PaddedSize);

			// "LVL_"
			var signature = reader.ReadFixed32();
			// Level Flags
			var flags = reader.ReadFixed32();
			// Level ID
			int levelId = (int)reader.ReadFixed32();
			// Item count (always 2^level)
			int levelCount = (int)reader.ReadFixed32();

			if (signature != SnapshotFormat.LEVEL_MAGIC_NUMBER) throw ParseError("Page does not appear to be a valid Level header");
			//TODO: check flags
			if (levelId != level) throw ParseError("Page contains the header of a different Level ({0} != {1})", levelId, level);
			if (levelCount != itemCount) throw ParseError("Item count ({0}) in level {1} header is not valid", levelCount, level);
			
			for (int i = 0; i < levelCount;i++)
			{
				// read the key
				uint keySize = reader.ReadVarint32();
				if (keySize > MemoryDatabaseHandler.MAX_KEY_SIZE) throw ParseError("Key size ({0}) is too big", keySize);
				USlice key = keySize == 0 ? USlice.Nil : reader.ReadBytes(keySize);

				// read the sequence
				ulong sequence = reader.ReadVarint64();

				// read the value
				uint valueSize = reader.ReadVarint32();
				if (valueSize > MemoryDatabaseHandler.MAX_VALUE_SIZE) throw ParseError("Value size ({0) is too big", valueSize);
				USlice value = valueSize == 0 ? USlice.Nil : reader.ReadBytes(valueSize);

				writer.Add(sequence, key, value);
			}

			if (reader.ReadFixed32() != uint.MaxValue) throw ParseError("Invalid end marker in level");
			//TODO: check end marker, CRC, ... ?
			uint checksum = reader.ReadFixed32();
			//TODO: verify checksum!
		}

	}


}
