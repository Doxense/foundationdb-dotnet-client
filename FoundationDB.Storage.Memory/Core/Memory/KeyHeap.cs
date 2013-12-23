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
				entry->Header = ((uint)EntryType.Key) << Entry.TYPE_SHIFT;

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

				Trace.WriteLine("## KeyPage: count=" + m_count.ToString("N0") + ", used=" + this.MemoryUsage.ToString("N0") + ", capacity=" + m_capacity.ToString("N0") + ", start=0x" + new IntPtr(m_start).ToString("X8") + ", end=0x" + new IntPtr(m_current).ToString("X8"));
				if (detailed)
				{
					while (current < end)
					{
						Trace.WriteLine("   - [" + Entry.GetObjectType(current).ToString() + "] 0x" + new IntPtr(current).ToString("X8") + " : " + current->Header.ToString("X8") + ", size=" + current->Size + " : " + FdbKey.Dump(Key.GetData(current).ToSlice()));
						var value = current->Values;
						while (value != null)
						{
							Trace.WriteLine("     -> [" + Entry.GetObjectType(value) + "] 0x" + new IntPtr(value).ToString("X8") + " @ " + value->Sequence + " : " + Value.GetData(value).ToSlice().ToAsciiOrHexaString());
							value = value->Previous;
						}
						current = Key.WalkNext(current);
					}
				}
			}
		}

		public KeyHeap(uint pageSize)
			: this(pageSize, pageSize)
		{ }

		public KeyHeap(uint minPageSize, uint maxPageSize)
			: base(minPageSize, maxPageSize, (handle, size) => new KeyHeap.Page(handle, size))
		{ }

		public Key* Append(USlice buffer)
		{
			Page page;
			var entry = (page = m_current) != null ? page.TryAppend(buffer) : null;
			if (entry == null)
			{ // allocate a new page and try again

				var size = buffer.Count + Key.SizeOf;

				var pageSize = Math.Max(Math.Min(m_pageSize << 1, m_minPageSize), m_maxPageSize);

				// if the key is larger than current page size, but we haven't yet reach the max, make it larger...
				while (size > pageSize && (pageSize << 1) < m_maxPageSize) { pageSize <<= 1; }
				m_pageSize = pageSize;

				page = CreateNewPage(pageSize, Entry.ALIGNMENT);
				m_current = page;
				m_pages.Add(page);
				entry = page.TryAppend(buffer);
			}
			if (entry == null) throw new OutOfMemoryException(String.Format("Failed to allocate memory from the heap for key of size {0}", buffer.Count));
			return entry;
		}

		public void Collect(ulong sequence)
		{
			//TODO: collect existing mages into large pages?
			var pageSize = m_minPageSize;
			m_pageSize = pageSize;

			foreach (var page in m_pages)
			{
				var target = CreateNewPage(pageSize, Entry.ALIGNMENT);
				page.Collect(target, sequence);
				page.Swap(target);
			}

		}

	}

}
