#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Linq;
	using System.Runtime.InteropServices;

	/// <summary>Generic implementation of an elastic heap that uses one or more page to store objects of the same type, using multiple buckets for different page sizes</summary>
	/// <typeparam name="TPage">Type of the pages in the elastic heap</typeparam>
	internal abstract class ElasticHeap<TPage> : IDisposable
		where TPage : EntryPage
	{
		private const uint MinAllowedPageSize = 4096; // ~= memory mapped page
		private const uint MaxAllowedPageSize = 1 << 30; // 1GB

		protected readonly TPage[] m_currents;
		protected readonly PageBucket[] m_buckets;
		protected Func<SafeHandle, uint, TPage> m_allocator;
		private volatile bool m_disposed;

		protected struct PageBucket
		{
			public readonly uint PageSize;
			public readonly List<TPage> Pages;
			public readonly List<TPage> FreeList;

			public PageBucket(uint size)
			{
				this.PageSize = size;
				this.Pages = new List<TPage>();
				this.FreeList = new List<TPage>();
			}
		}

		protected ElasticHeap(uint[] sizes, Func<SafeHandle, uint, TPage> allocator)
		{
			if (sizes == null) throw new ArgumentNullException("sizes");
			if (allocator == null) throw new ArgumentNullException("allocator");
			if (sizes.Length == 0) throw new ArgumentException("There must be at least one allocation size");

			var buckets = new PageBucket[sizes.Length];
			for (int i = 0; i < buckets.Length; i++)
			{
				if (sizes[i] < MinAllowedPageSize || sizes[i] > MaxAllowedPageSize) throw new ArgumentException(String.Format("Page size {0} too small or not a power of two", sizes[i]), "sizes");
				if (sizes[i] % Entry.ALIGNMENT != 0) throw new ArgumentException(String.Format("Page size {0} must be aligned to {1} bytes", sizes[i], Entry.ALIGNMENT));
				buckets[i] = new PageBucket(sizes[i]);
			}
			m_buckets = buckets;
			m_currents = new TPage[sizes.Length];
			m_allocator = allocator;
		}

		/// <summary>Allocate a new page for a specific bucket</summary>
		/// <param name="bucket">Bucet index</param>
		protected TPage CreateNewPage(int bucket)
		{
			uint size = m_buckets[bucket].PageSize;

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

		/// <summary>Returns the estimated allocated size in all the buckets</summary>
		public ulong GetAllocatedSize()
		{
			ulong sum = 0;
			foreach (var bucket in m_buckets)
			{
				if (bucket.PageSize > 0 && bucket.Pages != null)
				{
					sum += (ulong)bucket.PageSize * (uint)bucket.Pages.Count;
				}
			}
			return sum;
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
					for (int i = 0; i < m_buckets.Length; i++)
					{
						foreach (var page in m_buckets[i].Pages)
						{
							if (page != null) page.Dispose();
						}
						foreach (var page in m_buckets[i].FreeList)
						{
							if (page != null) page.Dispose();
						}
					}
					Array.Clear(m_buckets, 0, m_buckets.Length);
					Array.Clear(m_currents, 0, m_currents.Length);
				}
				m_allocator = null;
			}
		}

		[Conditional("DEBUG")]
		public void Debug_Dump(bool detailed)
		{
			Debug.WriteLine("# Dumping {0} heap ({1:N0} pages in {2:N0} buckets)", this.GetType().Name, m_buckets.Sum(b => (long)b.Pages.Count), m_buckets.Length);
			//TODO: needs locking but should only be called from unit tests anyway...
			ulong entries = 0;
			ulong allocated = 0;
			ulong used = 0;
			for (int i = 0; i < m_buckets.Length; i++)
			{
				var bucket = m_buckets[i];
				if (bucket.Pages == null) continue;
				if (bucket.Pages.Count == 0)
				{
					Debug.WriteLine(" # Bucket #{0}: {1:N0} bytes is empty", i, bucket.PageSize);
				}
				else
				{
					Debug.WriteLine(" # Bucket #{0}: {1:N0} bytes (allocated: {2:N0} pages, free: {3:N0} pages)", i, bucket.PageSize, bucket.Pages.Count, bucket.FreeList.Count);
					foreach (var page in bucket.Pages)
					{
						if (page == null) continue;
						page.Debug_Dump(detailed);
						allocated += bucket.PageSize;
						entries += (uint)page.Count;
						used += page.MemoryUsage;
					}
				}
			}
			Debug.WriteLine("# Found a total of {0:N0} entries using {1:N0} bytes out of {2:N0} bytes allocated", entries, used, allocated);
		}
	}

}
