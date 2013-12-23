#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.InteropServices;

	/// <summary>Generic implementation of an elastic heap that uses one or more page to store objects of the same type</summary>
	/// <typeparam name="TPage">Type of the pages in the elastic heap</typeparam>
	internal abstract class ElasticHeap<TPage> : IDisposable
		where TPage : EntryPage
	{
		private const uint MinAllowedPageSize = 64;
		private const uint MaxAllowedPageSize = 1 << 30; // 1GB

		/// <summary>List of all allocated pages</summary>
		protected readonly List<TPage> m_pages = new List<TPage>();

		/// <summary>Current page size</summary>
		protected uint m_pageSize;
		/// <summary>Minimum size of pages</summary>
		protected uint m_minPageSize;
		/// <summary>Maximum size of pages</summary>
		protected uint m_maxPageSize;

		/// <summary>Current page</summary>
		protected TPage m_current;

		protected Func<SafeHandle, uint, TPage> m_allocator;

		private volatile bool m_disposed;

		protected ElasticHeap(uint minPageSize, uint maxPageSize, Func<SafeHandle, uint, TPage> allocator)
		{
			if (minPageSize < MinAllowedPageSize) throw new ArgumentOutOfRangeException("minPageSize", minPageSize, "Minimum page size is too small");
			if (minPageSize > MaxAllowedPageSize) throw new ArgumentOutOfRangeException("minPageSize", minPageSize, "Minimum page size is too large");
			if (maxPageSize < minPageSize) throw new ArgumentOutOfRangeException("maxPageSize", maxPageSize, "Maximum page size cannot be less than minimum");
			if (maxPageSize > MaxAllowedPageSize) throw new ArgumentOutOfRangeException("maxPageSize", maxPageSize, "Maximum page size is too large");
			if (allocator == null) throw new ArgumentNullException("allocator");

			Trace.WriteLine("Created " + this.GetType().Name + " : min=" + minPageSize + "; max=" + maxPageSize);

			m_pageSize = 0;
			m_minPageSize = minPageSize;
			m_maxPageSize = maxPageSize;
			m_allocator = allocator;
		}

		protected TPage CreateNewPage(uint size, uint alignment)
		{
			// the size of the page should also be aligned
			var pad = size & (alignment - 1);
			if (pad != 0)
			{
				size += alignment - pad;
			}
			if (size > int.MaxValue)
			{
				throw new OutOfMemoryException("Cannot allocate page larger than 2GB");
			}

			UnmanagedHelpers.SafeLocalAllocHandle handle = null;
			try
			{
				handle = UnmanagedHelpers.AllocMemory(size);
				return m_allocator(handle, size);
			}
			catch (Exception e)
			{
				if (handle != null)
				{
					if (!handle.IsClosed) handle.Dispose();
					handle = null;
				}
				if (e is OutOfMemoryException)
				{
					throw new OutOfMemoryException(String.Format("Failed to allocate new memory for new page of size {0}", size), e);
				}
				throw;
			}
			finally
			{
				if (handle != null) GC.AddMemoryPressure(size);
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (disposing)
				{
					foreach(var page in m_pages)
					{
						if (page != null) page.Dispose();
					}
					m_pages.Clear();
				}
				m_current = null;
				m_allocator = null;
			}
		}

		[Conditional("DEBUG")]
		public void Debug_Dump(bool detailed)
		{
			Debug.WriteLine("# Dumping " + this.GetType().Name + " heap (" + m_pages.Count + " pages)");
			foreach(var page in m_pages)
			{
				page.Debug_Dump(detailed);
			}
		}
	}

}
