#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Utils
{
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>Allocate of unmanage memory pages</summary>
	[DebuggerDisplay("Used={m_memoryUsage}, PageSize={m_pageSize}, Pages={m_pages.Count}")]
	public unsafe sealed class UnmanagedMemoryHeap : IDisposable
	{
		// Allocator strategy:
		// To keep it simple, we have several pages that get filled one by one
		// If a page is too small to fit the next allocation, a new one is allocated
		// Large objects (more than half the size of the memory page) are allocated seperately on their own

		/// <summary>Default size for new pages</summary>
		private const uint DefaultPageSize = 1024 * 1024;

		/// <summary>Default alignment for pointers (note: 8 minimum)</summary>
		private static readonly uint DefaultAlignment = (uint) Math.Max(IntPtr.Size, 8);

		[DebuggerDisplay("Id={m_id}, Usage={this.Used} / {m_size}, Free={m_size-m_nextFree}, Ptr={m_handle}"), DebuggerTypeProxy(typeof(Page.DebugView))]
		internal sealed unsafe class Page : IDisposable
		{

			private readonly int m_id;
			private IntPtr m_handle;
			private uint m_size;

			private byte* m_begin;
			private uint m_nextFree;

			public Page(int id, IntPtr handle, uint size)
			{
				Contract.Requires(handle != IntPtr.Zero && size > 0);

				m_id = id;
				m_handle = handle;
				m_size = size;

				m_begin = (byte*)handle;

				// fill with zeroes !
				UnmanagedHelpers.FillUnsafe(m_begin, size, 0);

				GC.AddMemoryPressure(size);

				Contract.Ensures(m_handle != IntPtr.Zero && m_size > 0 && m_nextFree == 0);
			}

			~Page()
			{
				Dispose(false);
			}

			public byte* Start { get { return m_begin; } }

			public int Id { get { return m_id; } }

			public uint Size { get { return m_size; } }

			public uint Used { get { return m_nextFree; } }

			public uint Remaining { get { return m_size - m_nextFree; } }

			public bool Alive { get { return m_handle != IntPtr.Zero; } }

			private uint GetAlignmentOffset(uint alignment)
			{
				if (alignment <= 1) return 0;
				uint r = m_nextFree & (alignment - 1);
				return r == 0 ? 0 : (alignment - r);
			}

			public bool CanFit(uint size, uint alignment)
			{
				Contract.Requires(size > 0);

				return m_nextFree + size + GetAlignmentOffset(alignment) <= m_size;
			}

			public byte* Allocate(uint size, uint alignment)
			{
				Contract.Requires(size > 0);

				uint offset = GetAlignmentOffset(alignment);

				uint pos = m_nextFree + offset;
				byte* ptr = m_begin + pos;
				m_nextFree = pos + size;

				Contract.Ensures(ptr != null && ptr >= m_begin && ptr <= m_begin + m_size && m_nextFree <= m_size);
				return ptr;
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing)
			{
				try
				{
				}
				finally
				{
					GC.RemoveMemoryPressure(m_size);
					m_size = 0;
					m_begin = null;
					m_nextFree = 0;

					var handle = m_handle;
					if (handle != IntPtr.Zero) Marshal.FreeHGlobal(handle);
					m_handle = IntPtr.Zero;
				}
			}

			internal byte[] GetBytes()
			{
				if (m_handle == IntPtr.Zero) throw new ObjectDisposedException(this.GetType().Name);

				var tmp = new byte[this.Used];
				Marshal.Copy(m_handle, tmp, 0, tmp.Length);
				return tmp;
			}

			private sealed class DebugView
			{
				private readonly Page m_page;

				public DebugView(Page page)
				{
					m_page = page;
				}

				public uint Size { get { return m_page.Size; } }

				public byte[] Data
				{
					get { return m_page.GetBytes(); }
				}
			}

		}

		// HACKHACKHACK
		private readonly List<Page> m_pages = new List<Page>();

		/// <summary>Current page used by the heap</summary>
		private Page m_current = null;

		/// <summary>Default size for each memory page</summary>
		private readonly uint m_pageSize;

		/// <summary>Default pointer alignment</summary>
		private readonly uint m_alignment = DefaultAlignment;

		/// <summary>Total size of memory allocated from this heap</summary>
		private long m_memoryUsage;

		#region Constructors...

		public UnmanagedMemoryHeap()
			: this(0, 0)
		{ }

		public UnmanagedMemoryHeap(uint pageSize)
			: this(pageSize, 0)
		{ }

		public UnmanagedMemoryHeap(uint pageSize, uint alignment)
		{
			if (pageSize > (1 << 30)) throw new ArgumentOutOfRangeException("pageSize", "Page size cannot be larger than 1 GB");
			if (pageSize == 0) pageSize = DefaultPageSize;
			if (m_alignment == 0) m_alignment = DefaultAlignment;

			m_pageSize = pageSize;
			m_alignment = alignment;
		}

		#endregion

		#region Public Properties...

		public uint PageSize { get { return m_pageSize; } }

		public int PageCount { get { return m_pages.Count; } }

		internal IReadOnlyList<Page> Pages { get { return m_pages; } }

		public uint Alignment { get { return m_alignment; } }

		public long MemoryUsage { get { return m_memoryUsage; } }

		#endregion

		private Page AllocateNewPage(uint pageSize)
		{
			Page page;
			try
			{ }
			finally
			{
				var handle = IntPtr.Zero;
				try
				{
					Contract.Assert(pageSize <= 1 << 30);
					handle = Marshal.AllocHGlobal((int)pageSize);
					page = new Page(m_pages.Count, handle, pageSize);
				}
				catch (Exception)
				{
					if (handle != IntPtr.Zero) Marshal.FreeHGlobal(handle);
					throw;
				}

				m_memoryUsage += pageSize;
				m_pages.Add(page);
			}
			return page;
		}

		/// <summary>Allocate a new slice of unmanaged memory</summary>
		/// <param name="size">Size (in bytes) of the slice. Must be greater than zero.</param>
		/// <returns>Slice pointing to the newly allocated memory.</returns>
		public byte* Allocate(uint size)
		{
			// even though the caller don't require alignemnt, we still want to align to a multiple of 2 so that at least memory moves/cmps are aligned on a WORD boundary.
			return Allocate(size, 2);
		}

		public byte* AllocateAligned(uint size)
		{
			// align using the platform's pointer size (4 on x86, 8 on x64)
			return Allocate(size, m_alignment);
		}

		private byte* Allocate(uint size, uint align)
		{
			Contract.Requires(align == 1 || (align & (align - 1)) == 0); // only accept alignemnts that are a power of 2 !

			if (size == 0) throw new ArgumentOutOfRangeException("size", "Cannot allocate zero bytes");

			Page page;
			if (size > (m_pageSize >> 2))
			{ // big data go into its own page
				page = AllocateNewPage(size);
			}
			else
			{ // use the current page
				page = m_current;
				if (page == null || !page.CanFit(size, align))
				{ // need to allocate a new page
					page = AllocateNewPage(m_pageSize);
					m_current = page;
				}
			}
			Contract.Assert(page != null && page.Remaining >= size);

			byte* ptr = page.Allocate(size, align);
			if (ptr == null) throw new OutOfMemoryException();
			return ptr;
		}

		/// <summary>Copy the content of an unmanaged slice of memory</summary>
		/// <param name="data">Slice of unmanaged memory to copy</param>
		/// <returns>New slice pointing to the copied bytes in the allocator memory</returns>
		public USlice Memoize(USlice data)
		{
			return Memoize(data, 1);
		}

		/// <summary>Copy the content of an unmanaged slice of memory, starting at an aligned address</summary>
		/// <param name="data">Slice of unmanaged memory to copy</param>
		/// <returns>New slice pointing to the copied bytes in the allocator memory. The start address should be aligned to either 4 or 8 bytes, depending on the platform architecture.</returns>
		public USlice MemoizeAligned(USlice data)
		{
			return Memoize(data, m_alignment);
		}

		/// <summary>Copy the content of an unmanaged slice of memory, using a specific alignment</summary>
		/// <param name="data">Slice of unmanaged memory to copy</param>
		/// <param name="align">Required memory alignment. MUST BE A POWER OF 2 !</param>
		/// <returns>New slice pointing to the copied bytes in the allocator memory. The start address should be aligned to either 4 or 8 bytes, depending on the platform architecture.</returns>
		private USlice Memoize(USlice data, uint align)
		{
			if (data.Count == 0) return default(USlice);
			byte* ptr = Allocate(data.Count, align);
			if (ptr == null) throw new OutOfMemoryException();
			UnmanagedHelpers.CopyUnsafe(ptr, data);
			return new USlice(ptr, data.Count);
		}

		public USlice Memoize(Slice data)
		{
			return Memoize(data, 1);
		}

		public USlice MemoizeAligned(Slice data)
		{
			return Memoize(data, m_alignment);
		}

		private USlice Memoize(Slice data, uint align)
		{
			if (data.Count < 0 || data.Offset < 0) throw new InvalidOperationException("Cannot allocate less than zero bytes");
			if (data.Count == 0) return default(USlice);
			byte* ptr = Allocate((uint)data.Count, align);
			if (ptr == null) throw new OutOfMemoryException();
			Marshal.Copy(data.Array, data.Offset, new IntPtr(ptr), data.Count);
			return new USlice(ptr, (uint)data.Count);
		}
	
		public void Dispose()
		{
			foreach (var page in m_pages)
			{
				if (page.Alive) page.Dispose();
			}
			m_pages.Clear();

			GC.SuppressFinalize(this);
		}

		public void Dump(bool detailed = false)
		{
			Console.WriteLine("Dumping arena state:");
			long used = 0;
			foreach (var page in m_pages)
			{
				Console.WriteLine("- Page #" + page.Id + " (Used=" + page.Used + " / " + page.Size + ", " + (page.Remaining * 100.0 / page.Size).ToString("N1") + "% free)");
				used += page.Used;
				var data = page.GetBytes();
				if (detailed)
				{
					var sb = new StringBuilder(">");
					var txt = detailed ? new StringBuilder(32) : null;
					for (int i = 0; i < data.Length; i++)
					{
						byte b = data[i];
						sb.Append(' ').Append(b.ToString("X2"));
						if (detailed) txt.Append(b < 32 || b >= 254 ? '.' : (char)b);

						if (i % 32 == 31)
						{
							if (detailed) sb.Append("\t").Append(txt.ToString());
							txt.Clear();
							sb.Append("\r\n>");
						}
					}
					Console.WriteLine(sb.ToString());
				}
			}
			Console.WriteLine("> Memory usage: " + m_memoryUsage.ToString("N0") + " total, " + used.ToString("N0") + " used");
		}
	
		public void DumpToDisk(string path)
		{
			path = System.IO.Path.GetFullPath(path);
			Console.WriteLine("> Dumping heap content on disk ({0} bytes): {1}", m_memoryUsage, path);
			using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite, 4096, System.IO.FileOptions.None))
			{
				foreach (var page in m_pages)
				{
					var data = page.GetBytes();
					fs.Write(data, 0, data.Length);
				}
			}
		}
	}

}
