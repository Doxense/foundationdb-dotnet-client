#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.IO
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Storage.Memory.API;
	using FoundationDB.Storage.Memory.Core;
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Threading;
	using System.Threading.Tasks;
        using System.Runtime.InteropServices;
        
	internal class SnapshotWriter
	{
		private readonly Win32SnapshotFile m_file;
		private SliceWriter m_writer;

		private readonly int m_levels;
		private readonly int m_pageSize;
		private readonly int m_bufferSize;

		private Uuid128 m_uid;
		private ulong m_sequence;
		private long m_itemCount;
		private long m_timestamp;
		private uint m_headerChecksum;

		private readonly KeyValuePair<ulong, ulong>[] m_jumpTable;

		public SnapshotWriter(Win32SnapshotFile file, int levels, int pageSize, int bufferSize)
		{
			Contract.Requires(file != null && levels >= 0 && pageSize >= 0 && bufferSize >= pageSize); //TODO: && file.CanRead ?
			m_file = file;
			m_pageSize = pageSize;
			m_bufferSize = bufferSize;
			//TODO: verify pageSize is a power of two, and bufferSize is a multiple of pageSize!
			Contract.Assert(bufferSize % pageSize == 0);

			m_writer = new SliceWriter(bufferSize);
			m_levels = levels;

			m_jumpTable = new KeyValuePair<ulong, ulong>[levels];
			for (int i = 0; i < levels; i++)
			{
				m_jumpTable[i] = new KeyValuePair<ulong, ulong>(0, 0);
			}
		}

		/// <summary>Write the header to the file</summary>
		/// <param name="headerFlags"></param>
		/// <param name="uid"></param>
		/// <param name="sequence"></param>
		/// <param name="count"></param>
		/// <param name="timestamp"></param>
		/// <param name="attributes"></param>
		/// <remarks>This needs to be called before writing any level to the file</remarks>
		public Task WriteHeaderAsync(SnapshotFormat.Flags headerFlags, Uuid128 uid, ulong sequence, long count, long timestamp, IDictionary<string, IFdbTuple> attributes)
		{
			// The header will be use on ore more "pages", to simplify the job of loading / peeking at a stream content (no need for fancy buffering, just need to read 4K pages)
			// > The last page is padded with 0xAAs to detect corruption.

			m_uid = uid;
			m_sequence = sequence;
			m_itemCount = count;
			m_timestamp = timestamp;

			// HEADER
			// - DB_HEADER (64 bytes)
			// - DB ATTRIBUTES (variable size list of k/v)
			// - END_MARKER + HEADER_CRC
			// - PADDING (to fill last page)

			// DB Header

			// "PNDB"
			m_writer.WriteFixed32(SnapshotFormat.HEADER_MAGIC_NUMBER);
			// v1.0
			m_writer.WriteFixed16(1); // major
			m_writer.WriteFixed16(0); // minor
			// FLAGS
			m_writer.WriteFixed64((ulong)headerFlags);
			// Database ID
			m_writer.WriteBytes(uid.ToSlice());
			// Database Version
			m_writer.WriteFixed64(sequence);
			// Number of items in the database
			m_writer.WriteFixed64((ulong)count);
			// Database Timestamp
			m_writer.WriteFixed64((ulong)timestamp);
			// Page Size
			m_writer.WriteFixed32(SnapshotFormat.PAGE_SIZE);
			// Header Size (not known yet and will be filled in later)
			int offsetToHeaderSize = m_writer.Skip(4);

			// we should be at the 64 byte mark
			Contract.Assert(m_writer.Position == SnapshotFormat.HEADER_METADATA_BYTES);

			// DB Attributes
			m_writer.WriteFixed32((uint)attributes.Count);
			foreach (var kvp in attributes)
			{
				// Name
				m_writer.WriteVarbytes(Slice.FromString(kvp.Key));

				// Value
				m_writer.WriteVarbytes(kvp.Value.ToSlice());
			}

			// Mark the end of the header
			m_writer.WriteFixed32(uint.MaxValue);

			// we now have the size of the header, and can fill in the blank
			var headerEnd = m_writer.Position;
			m_writer.Position = offsetToHeaderSize;
			// write the header size (includes the CRC)
			m_writer.WriteFixed32((uint)checked(headerEnd + SnapshotFormat.HEADER_CRC_SIZE));
			m_writer.Position = headerEnd;

			// now we can compute the actual CRC
			uint headerChecksum = SnapshotFormat.ComputeChecksum(m_writer.ToSlice());
			m_writer.WriteFixed32(headerChecksum);
			m_headerChecksum = headerChecksum;

			// optional padding to fill the rest of the page
			PadPageIfNeeded(SnapshotFormat.PAGE_SIZE, 0xFD);

			return TaskHelpers.CompletedTask;
		}

		public async Task WriteLevelAsync(int level, IntPtr[] segment, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			if (m_jumpTable[level].Value > 0)
			{
				throw new InvalidOperationException("The level has already be written to this snapshot");
			}

			var levelStart = checked(m_file.Length + (uint)m_writer.Position);
			//Console.WriteLine("## level " + level + " starts at " + levelStart);

			//TODO: ensure that we start on a PAGE?

			//Console.WriteLine("> Writing level " + level);

			// "LVL_"
			m_writer.WriteFixed32(SnapshotFormat.LEVEL_MAGIC_NUMBER);
			// Level Flags
			m_writer.WriteFixed32(0); //TODO: flags!
			// Level ID
			m_writer.WriteFixed32((uint)level);
			// Item count (always 2^level)
			m_writer.WriteFixed32((uint)segment.Length);

			for (int i = 0; i < segment.Length; i++)
			{
				unsafe
				{
#if MONO
					var valuePointer =new IntPtr((void*) MemoryDatabaseHandler.ResolveValueAtVersion(segment[i], m_sequence));

					if (valuePointer == IntPtr.Zero)
						continue;

					Value value = new Value();
					Marshal.PtrToStructure(valuePointer, value);

					var keyPointer = new IntPtr((void*)segment[i]);

					Key key = new Key();
					Marshal.PtrToStructure(keyPointer, key);

					Contract.Assert(key.Size <= MemoryDatabaseHandler.MAX_KEY_SIZE);

					// Key Size
					uint size = key.Size;
					m_writer.WriteVarint32(size);
					m_writer.WriteBytesUnsafe(&(key.Data), (int)size);

					// Value
					m_writer.WriteVarint64(value.Sequence); // sequence
					size = value.Size;
					if (size == 0)
					{ // empty key
						m_writer.WriteByte(0);
					}
					else
					{
						m_writer.WriteVarint32(size); // value size
						m_writer.WriteBytesUnsafe(&(value.Data), (int)size); // value data
					}
#else

					Value* value = MemoryDatabaseHandler.ResolveValueAtVersion(segment[i], m_sequence);
					if (value == null)
					{
						continue;
					}
					Key* key = (Key*)segment[i]; //.ToPointer();

					Contract.Assert(key != null && key->Size <= MemoryDatabaseHandler.MAX_KEY_SIZE);

					// Key Size
					uint size = key->Size;
					m_writer.WriteVarint32(size);
					m_writer.WriteBytesUnsafe(&(key->Data), (int)size);

					// Value

					m_writer.WriteVarint64(value->Sequence); // sequence
					size = value->Size;
					if (size == 0)
					{ // empty key
						m_writer.WriteByte(0);
					}
					else
					{
						m_writer.WriteVarint32(size); // value size
						m_writer.WriteBytesUnsafe(&(value->Data), (int)size); // value data
					}
#endif
				}

				if (m_writer.Position >= SnapshotFormat.FLUSH_SIZE)
				{
					//Console.WriteLine("> partial flush (" + writer.Position + ")");
					int written = await m_file.WriteCompletePagesAsync(m_writer.Buffer, m_writer.Position, ct).ConfigureAwait(false);
					if (written > 0) m_writer.Flush(written);
				}
			}

			m_writer.WriteFixed32(uint.MaxValue);

			//TODO: CRC? (would need to be computed on the fly, because we don't have the full slice in memory probably)
			m_writer.WriteFixed32(0);

			var levelEnd = checked(m_file.Length + (uint)m_writer.Position);
			m_jumpTable[level] = new KeyValuePair<ulong, ulong>(levelStart, levelEnd - levelStart);
			//Console.WriteLine("## level " + level + " ends at " + levelEnd);

			// optional padding to fill the rest of the page
			PadPageIfNeeded(SnapshotFormat.PAGE_SIZE, (byte)(0xFC - level));
		}

		public Task WriteJumpTableAsync(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			// The jump table is the last page of the file
			// - it contains the list of (offset, size) of all the levels that are in the file
			// - it contains any additional attributes (that were only known after writing all the data)
			// - it repeats a few important values (sequence, header crc, ...)
			// - it would contain any optional signature or data that is only know after writing the data to disk, and are needed to decode the rest

			// marks the start of the JT because we will need to compute the checksum later on
			int startOffset = m_writer.Position;

			// "JMPT"
			m_writer.WriteFixed32(SnapshotFormat.JUMP_TABLE_MAGIC_NUMBER);
			// Page Size (repeated)
			m_writer.WriteFixed32((uint)m_pageSize);
			// Sequence Number (repeated)
			m_writer.WriteFixed64(m_sequence);
			// Database ID (repeated)
			m_writer.WriteBytes(m_uid.ToSlice());
			// Header CRC (repeated)
			m_writer.WriteFixed32(m_headerChecksum);

			int levels = m_levels;
			m_writer.WriteFixed32((uint)levels);			// Level Count
			for (int level = 0; level < levels; level++)
			{
				// Level Offset (from start of file)
				m_writer.WriteFixed64((ulong)m_jumpTable[level].Key);
				// Level Size (in bytes)
				m_writer.WriteFixed64((ulong)m_jumpTable[level].Value);
			}

			//TODO: additional attributes!
			m_writer.WriteFixed32(0); // 0 for now

			// End Marker
			m_writer.WriteFixed32(uint.MaxValue);

			// Checksum
			int endOffset = m_writer.Position;
			uint jumpTableChecksum = SnapshotFormat.ComputeChecksum(m_writer[startOffset, endOffset]);
			m_writer.WriteFixed32(jumpTableChecksum);

			// optional padding to fill the rest of the page
			PadPageIfNeeded(SnapshotFormat.PAGE_SIZE, 0xFE);

			// we are done !
			return TaskHelpers.CompletedTask;
		}

		public Task FlushAsync(CancellationToken ct)
		{
			//Console.WriteLine("> final flush (" + writer.Position + ")");
			return m_file.FlushAsync(m_writer.Buffer, m_writer.Position, ct);
		}

		private void PadPageIfNeeded(int pageSize, byte padByte)
		{
			// Ensure the page is full
			int pageOffset = m_writer.Position & (SnapshotFormat.PAGE_SIZE - 1);
			if (pageOffset != 0)
			{ // Pad the remainder of the page
				int pad = SnapshotFormat.PAGE_SIZE - pageOffset;
				m_writer.Skip(pad, padByte);
				//Console.WriteLine("@@@ added " + pad + " pad bytes => " + m_writer.Position);
			}
		}

	}

}
