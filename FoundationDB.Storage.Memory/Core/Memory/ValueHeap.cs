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

		public static Page CreateNewPage(uint size, uint alignment)
		{
			//Console.WriteLine("Created value page: " + size);
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

				entry->Header = ((uint)EntryType.Value) << Entry.TYPE_SHIFT;
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

			public override void Debug_Dump()
			{
				Contract.Requires(m_start != null && m_current != null);
				Value* current = (Value*)m_start;
				Value* end = (Value*)m_current;

				Trace.WriteLine("## ValuePage: count=" + m_count.ToString("N0") + ", used=" + this.MemoryUsage.ToString("N0") + ", capacity=" + m_capacity.ToString("N0") + ", start=0x" + new IntPtr(m_start).ToString("X8") + ", end=0x" + new IntPtr(m_current).ToString("X8"));

				while (current < end)
				{
					Trace.WriteLine("   - [" + Entry.GetObjectType(current).ToString() + "] 0x" + new IntPtr(current).ToString("X8") + " : " + current->Header.ToString("X8") + ", seq=" + current->Sequence + ", size=" + current->Size + " : " + Value.GetData(current).ToSlice().ToAsciiOrHexaString());
					if (current->Previous != null) Trace.WriteLine("     -> Previous: [" + Entry.GetObjectType(current->Previous) + "] 0x" + new IntPtr(current->Previous).ToString("X8"));
					if (current->Parent != null) Trace.WriteLine("     <- Parent: [" + Entry.GetObjectType(current->Parent) + "] 0x" + new IntPtr(current->Parent).ToString("X8"));

					current = Value.WalkNext(current);
				}
			}

		}

		public ValueHeap(uint pageSize)
			: base(pageSize)
		{ }

		public Value* Allocate(uint dataSize, ulong sequence, Value* previous, void* parent)
		{
			Page page;
			var entry = (page = m_current) != null ? page.TryAllocate(dataSize, sequence, previous, parent) : null;
			if (entry == null)
			{
				uint size = dataSize + Value.SizeOf;
				if (size > m_pageSize >> 1)
				{ // if the value is too big, it will use its own page

					page = CreateNewPage(size, Entry.ALIGNMENT);
					m_pages.Add(page);

				}
				else
				{ // allocate a new page and try again

					page = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
					m_current = page;
				}

				Contract.Assert(page != null);
				m_pages.Add(page);
				entry = page.TryAllocate(dataSize, sequence, previous, parent);
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

		}

	}

}
