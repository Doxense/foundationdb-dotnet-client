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

		public static Page CreateNewPage(uint size, uint alignment)
		{
			// the size of the page should also be aligned
			var pad = size & (alignment - 1);
			if (pad != 0)
			{
				size += alignment - pad;
			}
			if (size > int.MaxValue) throw new OutOfMemoryException();

			UnmanagedHelpers.SafeLocalAllocHandle handle = null;
			try
			{
				handle = UnmanagedHelpers.AllocMemory(size);
				return new Page(handle, size);
			}
			catch (Exception)
			{
				if (!handle.IsClosed) handle.Dispose();
				throw;
			}
		}

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

			public override void Debug_Dump()
			{
				Contract.Requires(m_start != null && m_current != null);
				Key* current = (Key*)m_start;
				Key* end = (Key*)m_current;

				Trace.WriteLine("## KeyPage: count=" + m_count.ToString("N0") + ", used=" + this.MemoryUsage.ToString("N0") + ", capacity=" + m_capacity.ToString("N0") + ", start=0x" + new IntPtr(m_start).ToString("X8") + ", end=0x" + new IntPtr(m_current).ToString("X8"));

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

		public KeyHeap(uint pageSize)
			: base(pageSize)
		{ }

		public Key* Append(USlice buffer)
		{
			Page page;
			var entry = (page = m_current) != null ? page.TryAppend(buffer) : null;
			if (entry == null)
			{
				if (buffer.Count > m_pageSize >> 1)
				{ // if the value is too big, it will use its own page

					page = CreateNewPage(buffer.Count, Entry.ALIGNMENT);
					m_pages.Add(page);
				}
				else
				{ // allocate a new page and try again

					page = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
					m_current = page;
				}

				Contract.Assert(page != null);
				m_pages.Add(page);
				entry = page.TryAppend(buffer);
				Contract.Assert(entry != null);
			}
			Contract.Assert(entry != null);
			return entry;
		}


		public void Collect(ulong sequence)
		{
			foreach (var page in m_pages)
			{
				var target = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
				page.Collect(target, sequence);
				page.Swap(target);
			}

		}

	}

}
