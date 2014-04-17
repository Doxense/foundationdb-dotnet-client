#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
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
		private List<KeyValuePair<long, long>> m_jumpTable;

		private Version m_version;
		private SnapshotFormat.Flags m_dbFlags;
		private Uuid m_uid;
		private ulong m_sequence;
		private long m_itemCount;
		private ulong m_timestamp;

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

			// minimum header prolog size is 64 but most will only a single 4K page
			// already preallocate the header page, and we will resize later if needed
			var bytes = new byte[Math.Min(SnapshotFormat.DB_INFO_BYTES, SnapshotFormat.PAGE_SIZE)];

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
			if (signature != SnapshotFormat.MAGIC_NUMBER) throw ParseError("invalid magic number");

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

			m_hasHeader = true;
		}

		public bool HasLevel(int level)
		{
			return m_hasJumpTable && level >= 0 && level < m_jumpTable.Count && m_jumpTable[level].Value != 0;
		}

		public async Task ReadJumpTableAsync(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			if (!m_hasHeader)
			{
				throw new InvalidOperationException("Cannot read the Jump Table without reading the Header first!");
			}

			throw new NotImplementedException();

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

			throw new NotImplementedException();
		}

	}

}
