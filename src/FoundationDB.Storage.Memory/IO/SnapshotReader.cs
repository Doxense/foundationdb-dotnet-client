#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.IO
{
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Threading;
	using System.Threading.Tasks;

	internal class SnapshotReader
	{
		private Win32SnapshotFile m_file;

		private bool m_hasHeader;
		private bool m_hasJumpTable;
		private KeyValuePair<long, long>[] m_jumpTable;

		private Version m_version;
		private SnapshotFormat.Flags m_dbFlags;
		private Uuid m_uid;
		private ulong m_sequence;
		private long m_itemCount;
		private ulong m_timestamp;
		private uint m_headerChecksum;

		private int m_pageSize;
		private int m_headerSize;

		private int m_levels;
		private byte[] m_page;

		public SnapshotReader(Win32SnapshotFile file)
		{
			Contract.Requires(file != null); //TODO: && file.CanRead ?
			m_file = file;
		}

		public int Depth
		{
			get { return m_levels; }
		}

		private Exception ParseError(string message)
		{
			return new InvalidOperationException(String.Format("Database snapshot is invalid or corrupted: {0}", message));
		}

		public async Task ReadHeaderAsync(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			// minimum header prolog size is 64 but most will only a single page
			// we can preallocate a full page, and we will resize it later if needed
			var bytes = m_page = new byte[SnapshotFormat.PAGE_SIZE];
			Contract.Assert(bytes.Length >= SnapshotFormat.DB_INFO_BYTES);

			int n = await m_file.ReadExactlyAsync(bytes, 0, SnapshotFormat.DB_INFO_BYTES, ct).ConfigureAwait(false);
			if (n != SnapshotFormat.DB_INFO_BYTES)
			{
				throw new InvalidOperationException("Invalid database snapshot: could not read the complete header");
			}

			var reader = SliceReader.FromBuffer(bytes);

			// "PNDB"
			var signature = reader.ReadFixed64();
			// v1.0
			uint major = reader.ReadFixed16();
			uint minor = reader.ReadFixed16();
			m_version = new Version((int)major, (int)minor);
			// FLAGS
			m_dbFlags = (SnapshotFormat.Flags) reader.ReadFixed64();
			// Database ID
			m_uid = new Uuid(reader.ReadBytes(16));
			// Database Version
			m_sequence = reader.ReadFixed64();
			// Number of items in the database
			m_itemCount = checked((long)reader.ReadFixed64());
			// Database Timestamp
			m_timestamp = reader.ReadFixed64();
			// Page Size
			m_pageSize = checked((int)reader.ReadFixed32());
			// Header Size
			m_headerSize = checked((int)reader.ReadFixed32());

			Contract.Assert(!reader.HasMore);

			#region Sanity checks

			// Signature
			if (signature != SnapshotFormat.DB_HEADER_MAGIC_NUMBER) throw ParseError("invalid magic number");

			// Version
			if (m_version.Major != 1) throw ParseError("unsupported file version (major)");
			if (m_version.Minor > 0) throw ParseError("unsupported file version (minor)");

			// Flags

			// Header Size
			if (m_headerSize < 64 + 4 + 4)
			{
				throw ParseError("Header size is too small");
			}
			if (m_headerSize > m_file.Length)
			{
				throw ParseError("Header size is bigger than the file itself");
			}
			if (m_headerSize > 1024 * 1024)
			{
				throw ParseError("Header size exceeds the maximum allowed size");
			}

			#endregion

			// we know the page size and header size, read the rest...

			if (bytes.Length < m_headerSize)
			{ // we need to resize
				var tmp = new byte[m_headerSize];
				Buffer.BlockCopy(bytes, 0, tmp, 0, bytes.Length);
				bytes = tmp;
				// reset the reader
				reader = SliceReader.FromBuffer(bytes);
				reader.Position = SnapshotFormat.DB_INFO_BYTES;
			}

			// read the rest
			n = await m_file.ReadExactlyAsync(bytes, SnapshotFormat.DB_INFO_BYTES, m_headerSize - SnapshotFormat.DB_INFO_BYTES, ct).ConfigureAwait(false);
			if (n != m_headerSize - SnapshotFormat.DB_INFO_BYTES)
			{
				throw ParseError("Could not read the complete header");
			}

			// parse the attributes
			Contract.Assert(reader.Position == SnapshotFormat.DB_INFO_BYTES);
			var attributeCount = checked((int)reader.ReadFixed32());

			//TODO!

			// verify the header checksum
			uint headerChecksum = 1234;
			m_headerChecksum = headerChecksum;

			uint actualHeaderChecksum = 1234;
			if (headerChecksum != actualHeaderChecksum)
			{
				throw ParseError("The header checksum does not match. This may be an indication of data corruption");
			}

			m_hasHeader = true;
		}

		public bool HasLevel(int level)
		{
			return m_hasJumpTable && level >= 0 && level < m_jumpTable.Length && m_jumpTable[level].Value != 0;
		}

		public async Task ReadJumpTableAsync(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			if (!m_hasHeader)
			{
				throw new InvalidOperationException("Cannot read the Jump Table without reading the Header first!");
			}

			// an empty database will have at least 2 pages: the header and the JT
			if (m_file.Length < checked(m_pageSize << 1))
			{
				throw ParseError("File size is too small to be a valid snapshot");
			}

			// the jumptable is always in the last page of the file and is expected to fit nicely
			// > file size MUST be evenly divible by page size
			// > then JT offset will be file.Length - pageSize
			if (m_file.Length % m_pageSize != 0)
			{
				throw ParseError("The file size is not a multiple of the page size, which may be a symptom of truncation");
			}

			long jumpTableStart = m_file.Length - m_pageSize;
			m_file.Seek(jumpTableStart);

			var bytes = m_page;
			if (bytes == null || bytes.Length < m_pageSize)
			{
				bytes = m_page = new byte[m_pageSize];
			}

			int n = await m_file.ReadExactlyAsync(bytes, 0, m_pageSize, ct).ConfigureAwait(false);
			if (n < m_pageSize) throw ParseError("Failed to read the complete Jump Table page");

			var reader = SliceReader.FromBuffer(bytes);

			// "JMPT"
			var signature = reader.ReadFixed32();
			// Page Size (repeated)
			var pageSizeRepeated = (int)reader.ReadFixed32();
			// Sequence Number (repeated)
			var sequenceRepeated = reader.ReadFixed64();
			// Database ID (repeated)
			var uidRepeated = new Uuid(reader.ReadBytes(16));
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

			var table = new KeyValuePair<long, long>[levels];
			for (int level = 0; level < levels; level++)
			{
				long offset = (long)reader.ReadFixed64();
				long size = (long)reader.ReadFixed64();

				// Size cannot be negative
				// Empty levels must have offset == -1
				// Non empty levels must have offset >= m_pageSize (or even >= m_headerSize ?)
				if (size < 0) throw ParseError("Level size in Jump Table cannot have a negative size");
				if ((size == 0 && offset != -1) || (size > 0 && offset < Math.Max(m_pageSize, m_headerSize))) throw ParseError("Level in Jump Table has invalid offset");

				table[level] = new KeyValuePair<long,long>(offset, size);
			}

			// end marker
			if (reader.ReadFixed32() != uint.MaxValue) throw ParseError("Jump Table end marker not found");

			// checksum
			uint observedChecksum = SnapshotFormat.ComputeChecksum(reader.Head);
			uint jumpTableChecksum = reader.ReadFixed32();
			if (observedChecksum != jumpTableChecksum) throw ParseError("Jump Table checksum does not match. This may be an indication of data corruption");

			m_jumpTable = table;
			m_levels = levels;
			m_hasJumpTable = true;
		}

		public async Task<bool> ReadLevelAsync(int level, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			if (!m_hasJumpTable)
			{
				throw new InvalidOperationException("Cannot read a level without reading the Jump Table first!");
			}

			long levelOffset = m_jumpTable[level].Key;
			long levelSize = m_jumpTable[level].Value;

			if (levelOffset < m_headerSize || levelOffset > m_file.Length)
			{
				throw ParseError("Level offset is invalid");
			}
			if (levelSize < 0 || checked(levelOffset + levelSize) > m_file.Length)
			{
				throw ParseError("Level size is invalid");
			}

			m_file.Seek(levelOffset);

			//TODO: stream and read the data as fast as possible

			throw new NotImplementedException();
		}

	}

}
