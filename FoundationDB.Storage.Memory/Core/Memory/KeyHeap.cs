#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Client;
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;

	internal unsafe class KeyHeap : ElasticHeap<KeyHeap.Page>
	{

		// Some facts about keys:
		// - The overhead per key is 16 bytes on x64
		// - The maximum allowed size for a key is 10,000 bytes.
		// - Well designed layers will tend to use small keys.
		// - Text-based indexes may need longer keys.
		// - Very large keys should be rare and will already be slow due to longer memcmps anyway.
		// - The smallest possible entry will be 16 bytes (empty key) which can occur only once per database
		// - A typical small key "(42, small_int)" will be ~24 bytes
		//   - Page size of   4 KB can fit  170 keys with waste ~ 0.4%
		//   - Page size of  16 KB can fit  682 keys with waste ~ 0.1%
		//   - Page size of  64 KB can fit 2730 keys with waste negligible
		// - A typical index composite key "(42, GUID, TimeStamp, int16)" will be ~48 bytes
		//   - Page size of   4 KB can fit   85 keys with waste ~ 0.4%
		//   - Page size of  16 KB can fit  341 keys with waste ~ 0.1%
		//   - Page size of  64 KB can fit 1365 keys with waste negligible
		// - A somewhat longer key "(42, 1, GUID, GUID, TimeStamp, int16)" will be ~64 bytes
		//   - Page size of   4 KB can fit   64 keys with no waste
		//   - Page size of  16 KB can fit  256 keys with no waste
		//   - Page size of  64 KB can fit 1024 keys with no waste
		// - A "big" key will be ~1000 bytes and should be pretty rare (either very specific scenario, or badly designed Layer)
		//   - Page size of   4 KB can fit    4 keys with waste ~ 2.3%
		//   - Page size of  16 KB can fit   16 keys with waste ~ 2.3%
		//   - Page size of  64 KB can fit   64 keys with waste ~ 0.8%
		//   - Page size of 128 KB can fit  128 keys with waste negligible
		// - The largest possible entry size is 10,016 bytes and should never happen in well designed Layers
		//   - Page size smaller than 16KB are not possible (too small)
		//   - Page size of  16 KB can fit    1 key  with a waste of 6368 bytes (38.8%)
		//   - Page size of  32 KB can fit    3 keys with a waste of 2720 bytes ( 8.3%)
		//   - Page size of  64 KB can fit    6 keys with a waste of 5440 bytes ( 8.3%)
		//   - Page size of 128 KB can fit   13 keys with a waste of  864 bytes ( 0.6%)
		//   - Page size of 256 KB can fit   26 keys with a waste of 1728 bytes ( 0.6%)
		//   - Page size of   1 MB can fit  104 keys with a waste of 6912 bytes ( 0.6%)

		// We should probably optimize for keys up to ~100 bytes, and try our best for longer keys.
		// => We will use 4 buckets for the pages, and try to have at least 256 entries per page
		// - SMALL : keys up to     64 bytes, with page size of  16 KB
		// - MEDIUM: keys up to    256 bytes, with page size of  64 KB
		// - LARGE : keys up to  1,024 bytes, with page size of 256 KB
		// - HUGE  : keys up to 10,016 bytes, with page size of   1 MB (fit up to 104 entries)

		/// <summary>Page of memory used to store Keys</summary>
		public sealed unsafe class Page : EntryPage
		{

			public Page(SafeHandle handle, uint capacity)
				: base(handle, capacity)
			{ }

			public override EntryType Type
			{
				get { return EntryType.Key; }
			}

			/// <summary>Copy an existing value to this page, and return the pointer to the copy</summary>
			/// <param name="value">Value that must be copied to this page</param>
			/// <returns>Pointer to the copy in this page</returns>
			public Key* TryAppend(Key* value)
			{
				Contract.Requires(value != null && Entry.GetObjectType(value) == EntryType.Value);

				uint rawSize = Key.SizeOf + value->Size;
				var entry = (Key*)TryAllocate(rawSize);
				if (entry == null) return null; // this page is full

				UnmanagedHelpers.CopyUnsafe((byte*)entry, (byte*)value, rawSize);

				return entry;
			}

			public Key* TryAppend(USlice buffer)
			{
				Contract.Requires(buffer.Data != null
					&& buffer.Count >= Key.SizeOf
					&& ((Key*)buffer.Data)->Size == buffer.Count - Key.SizeOf);

				var entry = (Key*)TryAllocate(buffer.Count);
				if (entry == null) return null; // this page is full

				UnmanagedHelpers.CopyUnsafe((byte*)entry, buffer.Data, buffer.Count);
				entry->Header = ((ushort)EntryType.Key) << Entry.TYPE_SHIFT;

				return entry;
			}

			public void Collect(KeyHeap.Page target, ulong sequence)
			{
				var current = (Key*)m_start;
				var end = (Key*)m_current;

				while (current < end)
				{
					bool keep = Key.StillAlive(current, sequence);

					if (keep)
					{ // copy to the target page

						var moved = target.TryAppend(current);
						if (moved == null) throw new InvalidOperationException("The target page was too small");

						var values = current->Values;
						if (values != null)
						{
							values->Parent = moved;
						}

						current->Header |= Entry.FLAGS_MOVED | Entry.FLAGS_DISPOSED;
					}
					else
					{
						current->Header |= Entry.FLAGS_DISPOSED;
					}

					current = Key.WalkNext(current);
				}


			}

			public override void Debug_Dump(bool detailed)
			{
				Contract.Requires(m_start != null && m_current != null);
				Key* current = (Key*)m_start;
				Key* end = (Key*)m_current;

				Trace.WriteLine("  # KeyPage: count=" + m_count.ToString("N0") + ", used=" + this.MemoryUsage.ToString("N0") + ", capacity=" + m_capacity.ToString("N0") + ", start=0x" + new IntPtr(m_start).ToString("X8") + ", end=0x" + new IntPtr(m_current).ToString("X8"));
				if (detailed)
				{
					while (current < end)
					{
						Trace.WriteLine("    - [" + Entry.GetObjectType(current).ToString() + "] 0x" + new IntPtr(current).ToString("X8") + " : " + current->Header.ToString("X8") + ", size=" + current->Size + ", h=0x" + current->HashCode.ToString("X4") + " : " + FdbKey.Dump(Key.GetData(current).ToSlice()));
						var value = current->Values;
						while (value != null)
						{
							Trace.WriteLine("      -> [" + Entry.GetObjectType(value) + "] 0x" + new IntPtr(value).ToString("X8") + " @ " + value->Sequence + " : " + Value.GetData(value).ToSlice().ToAsciiOrHexaString());
							value = value->Previous;
						}
						current = Key.WalkNext(current);
					}
				}
			}
		}

		private const int NUM_BUCKETS = 4;
		private const uint SMALL_KEYS = 64;
		private const uint MEDIUM_KEYS = 256;
		private const uint LARGE_KEYS = 1024;
		private const uint HUGE_KEYS = uint.MaxValue; // should nether be larger than 10,016 bytes

		private static readonly uint[] KeySizes = new uint[NUM_BUCKETS] {
			SMALL_KEYS,
			MEDIUM_KEYS,
			LARGE_KEYS,
			HUGE_KEYS
		};

		private static readonly uint[] PageSizes = new uint[NUM_BUCKETS]
		{
			/* SMALL  */   16 * 1024,
			/* MEDIUM */   64 * 1024,
			/* LARGE  */  256 * 1024,
			/* HUGE   */ 1024 * 1024
		};

		public KeyHeap()
			: base(PageSizes, (handle, size) => new KeyHeap.Page(handle, size))
		{ }

		private static int GetBucket(uint size)
		{
			if (size <= SMALL_KEYS) return 0;
			if (size <= MEDIUM_KEYS) return 1;
			if (size <= LARGE_KEYS) return 2;
			return 3;
		}

		public Key* Append(USlice buffer)
		{
			int bucket = GetBucket(buffer.Count + Key.SizeOf);

			var page = m_currents[bucket];
			var entry = page != null ? page.TryAppend(buffer) : null;
			if (entry == null)
			{  // allocate a new page and try again
				entry = AppendSlow(bucket, buffer);
			}
			return entry;
		}

		private Key* AppendSlow(int bucket, USlice buffer)
		{
			var page = CreateNewPage(bucket);
			Contract.Assert(page != null);
			m_currents[bucket] = page;
			m_buckets[bucket].Pages.Add(page);

			var entry = page.TryAppend(buffer);
			if (entry == null) throw new OutOfMemoryException(String.Format("Failed to allocate memory from the key heap ({0})", m_buckets[bucket].PageSize));
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
		}

	}

}
