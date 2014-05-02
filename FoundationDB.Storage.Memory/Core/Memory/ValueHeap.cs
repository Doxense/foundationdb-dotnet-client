#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;

	internal unsafe class ValueHeap : ElasticHeap<ValueHeap.Page>
	{

		// Some facts about values:
		// - The overhead per value is 32 bytes on x64
		// - The largest possible value size is 100,000 bytes
		// - A lot of layers (indexes, ...) use empty keys which could be optimized away and not take any space.
		// - Document layers that split documents into a field per key will use values from 1 or 2 bytes (bool, ints) to ~64 bytes (strings, labels, text GUIDs, ...)
		// - Some layers may pack complete documents in keys, or pack arrays, which will occupy a couple of KB
		// - Blob-type layers will need to split very large documents (files, pictures, logs, ...) into as few chunks as possible, and be pegged at 10,000 or 100,000 bytes
		// - A typical small value is an 32-bit or 64-bit integer or counter, which will be padded to 40 bytes
		//   - Page size of   4 KB can fit  170 keys with waste ~ 0.4%
		//   - Page size of  16 KB can fit  682 keys with waste ~ 0.1%
		//   - Page size of  64 KB can fit 2730 keys with waste negligible
		// - A GUID will be 48 bytes
		//   - Page size of   4 KB can fit   85 keys with waste ~ 0.4%
		//   - Page size of  16 KB can fit  341 keys with waste ~ 0.1%
		//   - Page size of  64 KB can fit 1365 keys with waste negligible
		// - A very small JSON doc {Id:"..",Value:"...",Tag:".."} will be less than ~128 bytes
		//   - Page size of   4 KB can fit   85 keys with waste ~ 0.4%
		// - An array of 60 doubles will be 512 bytes
		//   - Page size of   4 KB can fit   85 keys with waste ~ 0.4%
		// - A "small" chunk of a blob layer will be ~16K
		//   - Page size of  16 KB can fit    1 key  with no waste
		//   - Page size of  64 KB can fit   64 keys with waste ~ 0.8%
		//   - Page size of 128 KB can fit  128 keys with waste negligible
		// - The largest possible key is 10,032 bytes (header + pointer + 10,000 bytes) and should never happen in well designed Layers
		//   - Page size smaller than 16KB are not possible (too small)
		//   - Page size of  16 KB can fit    1 key  with a waste of 6368 bytes (38.8%)
		//   - Page size of  32 KB can fit    3 keys with a waste of 2720 bytes ( 8.3%)
		//   - Page size of  64 KB can fit    6 keys with a waste of 5440 bytes ( 8.3%)
		//   - Page size of 128 KB can fit   13 keys with a waste of  864 bytes ( 0.6%)
		//   - Page size of 256 KB can fit   26 keys with a waste of 1728 bytes ( 0.6%)
		//   - Page size of   1 MB can fit  104 keys with a waste of 6912 bytes ( 0.6%)

		// pb: layers wanting to target a size that is a power of two (1K, 2K, 16K, ...) will always be misaligned due to the 32 bytes overhead and may create waste in pages (especially small pages)

		// We should probably optimize for keys up to ~100 bytes, and try our best for longer keys.
		// => We will use 4 buckets for the pages, and try to have at least 256 entries per page
		// - SMALL : keys up to     64 bytes, with page size of  16 KB
		// - MEDIUM: keys up to    256 bytes, with page size of  64 KB
		// - LARGE : keys up to  1,024 bytes, with page size of 256 KB
		// - HUGE  : keys up to 10,016 bytes, with page size of   1 MB (fit up to 104 entries)

		/// <summary>Page of memory used to store Values</summary>
		public sealed class Page : EntryPage
		{

			public Page(SafeHandle handle, uint capacity)
				: base(handle, capacity)
			{ }

			public override EntryType Type
			{
				get { return EntryType.Value; }
			}

			/// <summary>Copy an existing value to this page, and return the pointer to the copy</summary>
			/// <param name="value">Value that must be copied to this page</param>
			/// <returns>Pointer to the copy in this page</returns>
			public Value* TryAppend(Value* value)
			{
				Contract.Requires(value != null && Entry.GetObjectType(value) == EntryType.Value);

				uint rawSize = Value.SizeOf + value->Size;
				Value* entry = (Value*)TryAllocate(rawSize);
				if (entry == null) return null; // the page is full

				UnmanagedHelpers.CopyUnsafe((byte*)entry, (byte*)value, rawSize);

				return entry;
			}

			public Value* TryAppend(USlice buffer)
			{
				Contract.Requires(buffer.Data != null
					&& buffer.Count >= Value.SizeOf
					&& ((Key*)buffer.Data)->Size == buffer.Count - Value.SizeOf);

				var entry = (Value*)TryAllocate(buffer.Count);
				if (entry == null) return null; // the page is full
				UnmanagedHelpers.CopyUnsafe((byte*)entry, buffer.Data, buffer.Count);

				return entry;
			}

			public Value* TryAllocate(uint dataSize, ulong sequence, Value* previous, void* parent)
			{
				Value* entry = (Value*)TryAllocate(Value.SizeOf + dataSize);
				if (entry == null) return null; // the page is full

				entry->Header = ((ushort)EntryType.Value) << Entry.TYPE_SHIFT;
				entry->Size = dataSize;
				entry->Sequence = sequence;
				entry->Previous = previous;
				entry->Parent = parent;

				return entry;
			}

			public void Collect(Page target, ulong sequence)
			{
				var current = (Value*)m_start;
				var end = (Value*)m_current;

				while (current < end)
				{
					bool keep = Value.StillAlive(current, sequence);

					void* parent = current->Parent;

					if (keep)
					{ // copy to the target page

						var moved = target.TryAppend(current);
						if (moved == null) throw new InvalidOperationException(); // ??

						// update the parent
						switch (Entry.GetObjectType(parent))
						{
							case EntryType.Key:
							{
								((Key*)parent)->Values = moved;
								break;
							}
							case EntryType.Value:
							{
								((Value*)parent)->Previous = moved;
								break;
							}
							case EntryType.Free:
							{
								//NO-OP
								break;
							}
							default:
							{
								throw new InvalidOperationException("Unexpected parent while moving value");
							}
						}
						current->Header |= Entry.FLAGS_MOVED | Entry.FLAGS_DISPOSED;
					}
					else
					{
						// we need to kill the link from the parent
						switch (Entry.GetObjectType(parent))
						{
							case EntryType.Key:
							{
								((Key*)parent)->Values = null;
								break;
							}
							case EntryType.Value:
							{
								((Value*)parent)->Previous = null;
								break;
							}
							case EntryType.Free:
							{
								//NO-OP
								break;
							}
							default:
							{
								throw new InvalidOperationException("Unexpected parent while destroying value");
							}
						}

						current->Header |= Entry.FLAGS_DISPOSED;
					}

					current = Value.WalkNext(current);
				}
			}

			public override void Debug_Dump(bool detailed)
			{
				Contract.Requires(m_start != null && m_current != null);
				Value* current = (Value*)m_start;
				Value* end = (Value*)m_current;

				Trace.WriteLine("  # ValuePage: count=" + m_count.ToString("N0") + ", used=" + this.MemoryUsage.ToString("N0") + ", capacity=" + m_capacity.ToString("N0") + ", start=0x" + new IntPtr(m_start).ToString("X8") + ", end=0x" + new IntPtr(m_current).ToString("X8"));
				if (detailed)
				{
					while (current < end)
					{
						Trace.WriteLine("    - [" + Entry.GetObjectType(current).ToString() + "] 0x" + new IntPtr(current).ToString("X8") + " : " + current->Header.ToString("X8") + ", seq=" + current->Sequence + ", size=" + current->Size + " : " + Value.GetData(current).ToSlice().ToAsciiOrHexaString());
						if (current->Previous != null) Trace.WriteLine("      -> Previous: [" + Entry.GetObjectType(current->Previous) + "] 0x" + new IntPtr(current->Previous).ToString("X8"));
						if (current->Parent != null) Trace.WriteLine("      <- Parent: [" + Entry.GetObjectType(current->Parent) + "] 0x" + new IntPtr(current->Parent).ToString("X8"));

						current = Value.WalkNext(current);
					}
				}
			}

		}

		private const int NUM_BUCKETS = 5;

		//note: we try to target more than 100 entries per page to reduce overhead and possible waste

		private const int TINY_VALUES = 16 + 32; // note (GUIDs or smaller)
		private const uint SMALL_VALUES = 128 + 32; // a tiny JSON doc should fit without problem
		private const uint MEDIUM_VALUES = 60 * 8 + 32; // an array of 60 doubles
		private const uint LARGE_VALUES = 4096 + 32; // a small size JSON doc (possibly compressed)
		private const uint HUGE_VALUES = uint.MaxValue;	// > 2KB would be "large documents", chunks of very large documents, or binary blobs

		private static readonly uint[] KeySizes = new uint[NUM_BUCKETS] {
			TINY_VALUES,
			SMALL_VALUES,
			MEDIUM_VALUES,
			LARGE_VALUES,
			HUGE_VALUES
		};

		private static readonly uint[] PageSizes = new uint[NUM_BUCKETS]
		{
			/* TINY   */       16 * 1024, // from 341 to  512 per page
			/* SMALL  */       64 * 1024, // from 409 to 1337 per page
			/* MEDIUM */      128 * 1024, // from 256 to  814 per page
			/* LARGE  */      256 * 1024, // from  63 to  511 per page
			/* HUGE   */     1024 * 1024, // from  10 to  253 per page
		};

		public ValueHeap()
			: base(PageSizes, (handle, size) => new ValueHeap.Page(handle, size))
		{ }

		private static int GetBucket(uint size)
		{
			if (size <= TINY_VALUES) return 0;
			if (size <= SMALL_VALUES) return 1;
			if (size <= MEDIUM_VALUES) return 2;
			if (size <= LARGE_VALUES) return 3;
			return 4;
		}

		public Value* Allocate(uint dataSize, ulong sequence, Value* previous, void* parent)
		{
			int bucket = GetBucket(dataSize + Value.SizeOf);

			var page = m_currents[bucket];
			var entry = page != null ? page.TryAllocate(dataSize, sequence, previous, parent) : null;
			if (entry == null)
			{
				entry = AllocateSlow(bucket, dataSize, sequence, previous, parent);
			}
			return entry;
		}

		private Value* AllocateSlow(int bucket, uint dataSize, ulong sequence, Value* previous, void* parent)
		{
			var page = CreateNewPage(bucket);
			Contract.Assert(page != null);
			m_currents[bucket] = page;
			m_buckets[bucket].Pages.Add(page);

			var entry = page.TryAllocate(dataSize, sequence, previous, parent);
			if (entry == null) throw new OutOfMemoryException(String.Format("Failed to allocate memory from the the value heap ({0})", m_buckets[bucket].PageSize));
			return entry;
		}

		public void Collect(ulong sequence)
		{
			for (int bucket = 0; bucket < m_buckets.Length; bucket++)
			{
				if (m_buckets[bucket].Pages.Count > 0)
				{
					//TODO:!!!
					//- allocate a scratch page
					//- for all pages in bucket that have more than x% of free space
					//  - copy as many surviving keys into scratch page
					//    - if scratch page is too small, add it to the list, allocate new scratch page (note: from the free list?)
					//  - put page into "free list"
				}
			}

#if REFACTORED
			foreach (var page in m_pages)
			{
				var target = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
				if (page.Count == 1)
				{ // this is a standalone page
					page.Collect(target, sequence);
					page.Swap(target);
				}
				else
				{
					page.Collect(target, sequence);
					page.Swap(target);
				}
			}
#endif
		}

	}

}
