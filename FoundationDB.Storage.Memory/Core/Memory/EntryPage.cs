#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;
	using System.Threading;

	/// <summary>Base implementation of a page of memory that can store items of the same type</summary>
	[DebuggerDisplay("Start={m_start}, Current={m_current}, Entries={m_count}, Usage={(m_current-m_start)} / {m_capacity}")]
	internal unsafe abstract class EntryPage : IDisposable
	{
		/// <summary>Pointer to the next free slot in the page</summary>
		protected byte* m_current;
		/// <summary>Pointer to the first byte of the page</summary>
		protected byte* m_start;
		/// <summary>Pointer to the next byte after the last byte of the page</summary>
		protected byte* m_end;
		/// <summary>Size of the page</summary>
		protected uint m_capacity;
		/// <summary>Number of entries stored in this page</summary>
		protected int m_count;
		/// <summary>Handle to the allocated memory</summary>
		protected SafeHandle m_handle;

		public EntryPage(SafeHandle handle, uint capacity)
		{
			Contract.Requires(handle != null && !handle.IsInvalid && !handle.IsClosed);

			m_handle = handle;
			m_capacity = capacity;
			m_start = (byte*) handle.DangerousGetHandle().ToPointer();
			m_end = m_start + capacity;
			m_current = m_start;
			CheckInvariants();
		}

		~EntryPage()
		{
			Dispose(false);
		}

		[Conditional("DEBUG")]
		protected void CheckInvariants()
		{
			Contract.Assert(!m_handle.IsInvalid, "Memory handle should not be invalid");
			Contract.Assert(!m_handle.IsClosed, "Memory handle should not be closed");
			Contract.Ensures(Entry.IsAligned(m_current), "Current pointer should always be aligned");
			Contract.Assert(m_current <= m_start + m_capacity, "Current pointer should never be outside the page");
		}

		/// <summary>Number of entries store in this page</summary>
		public int Count { get { return m_count; } }

		/// <summary>Number of bytes allocated inside this page</summary>
		public ulong MemoryUsage { get { return (ulong)(m_current - m_start); } }

		/// <summary>Type of the entries stored in this page</summary>
		public abstract EntryType Type { get; }

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
					var handle = m_handle;
					if (handle != null && !handle.IsClosed)
					{
						m_handle.Close();
						GC.RemoveMemoryPressure(m_capacity);
					}
				}
			}
			finally
			{
				m_handle = null;
				m_start = null;
				m_current = null;
			}
		}

		private void ThrowDisposed()
		{
			throw new ObjectDisposedException(this.GetType().Name);
		}

		/// <summary>Align a pointer in this page</summary>
		/// <param name="ptr">Unaligned location in the page</param>
		/// <param name="end">Aligned pointer cannot be greater than or equal to this address</param>
		/// <returns>New pointer that is aligned, and is guaranteed to be less than the <paramref name="end"/></returns>
		internal static byte* Align(byte* ptr, byte* end)
		{
			long r = ((long)ptr) & (Entry.ALIGNMENT - 1);
			if (r > 0) ptr += Entry.ALIGNMENT - r;
			if (ptr > end) return end;
			return ptr;
		}

		/// <summary>Try to allocate a segment in this page</summary>
		/// <param name="size">Minimum size of the segment</param>
		/// <returns>Pointer to the start of the allocated segment, or null if this page cannot satisfy the allocation</returns>
		/// <remarks>The pointer will be aligned before being returned. The method may return null even if there was enough space remaining, if the aligment padding causes the segment to overshoot the end of the page.</remarks>
		protected byte* TryAllocate(uint size)
		{
			// try to allocate an amount of memory
			// - returns null if the page is full, or too small
			// - returns a pointer to the allocated space

			byte* ptr = m_current;
			if (ptr == null) ThrowDisposed();
			byte* end = m_end;
			byte* next = ptr + size;
			if (next > m_end)
			{ // does not fit in this page
				return null;
			}

			// update the cursor for the next value
			next = (byte*) (((long)next + Entry.ALIGNMENT - 1) & Entry.ALIGNMENT_MASK);
			if (next > end) next = end;
			m_current = next;
			++m_count;

			CheckInvariants();
			return ptr;
		}

		/// <summary>Update this instance to use another memory location, and release the previously allocated memory</summary>
		/// <param name="target">Page that will be absorbed</param>
		/// <remarks>The content of the current page will be deleted, and <paramref name="target"/> will be disposed</remarks>
		public void Swap(EntryPage target)
		{
			if (target == null) throw new ArgumentNullException("target");
			Contract.Requires(target.m_handle != null);

			if (m_current == null) ThrowDisposed();
			if (target.m_current == null) target.ThrowDisposed();

			try
			{ }
			finally
			{
				var old = m_handle;
				m_handle = Interlocked.Exchange(ref target.m_handle, null);
				m_count = target.m_count;
				m_capacity = target.m_capacity;
				m_start = target.m_start;
				m_end = target.m_end;
				m_current = target.m_current;

				old.Dispose();
				target.Dispose();
			}
			CheckInvariants();
		}

		[Conditional("DEBUG")]
		public abstract void Debug_Dump(bool detailed);
	}

}
