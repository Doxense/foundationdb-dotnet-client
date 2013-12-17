#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Utils;
	using FoundationDB.Storage.Memory.Core;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal unsafe struct Entry
	{
		/// <summary>Default alignement for objects (8 by default)</summary>
		public const int ALIGNMENT = 8; // MUST BE A POWER OF 2 !
		public const int ALIGNMENT_MASK = ~(ALIGNMENT - 1);

		/// <summary>This entry has been moved to another page by the last GC</summary>
		public const uint FLAGS_MOVED = 0x100;

		/// <summary>This key has been flaged as being unreachable by current of future transaction (won't survive the next GC)</summary>
		public const uint FLAGS_UNREACHABLE = 0x2000;

		/// <summary>The entry has been disposed and should be access anymore</summary>
		public const uint FLAGS_DISPOSED = 0x80000000;

		public const int TYPE_SHIFT = 29;
		public const uint TYPE_MASK_AFTER_SHIFT = 0x3;

		// Object Layout
		// ==============

		// Offset	Field	Type	Desc
		// 
		//      0	HEADER	uint	Type, Flags, ...
		//		4	SIZE	uint	Size of the data
		//		... object fields ...
		//		x	DATA	byte[]	Value of the object, size in the SIZE field
		//		y	(pad)	0..7	padding bytes (set to 00 or FF ?)
		//
		// HEADER: bit flags
		// - bit 31: DISPOSED, set if object is disposed
		// - bit 29-30: TYPE

		/// <summary>Various flags (TODO: enum?)</summary>
		public uint Header;

		/// <summary>Size of the key (in bytes)</summary>
		public uint Size;

		/// <summary>Return the type of the object</summary>
		public static unsafe EntryType GetObjectType(void* item)
		{
			return item == null ? EntryType.Free : (EntryType)((((Entry*)item)->Header >> TYPE_SHIFT) & TYPE_MASK_AFTER_SHIFT);
		}

		/// <summary>Checks if the object is disposed</summary>
		public static unsafe bool IsDisposed(void* item)
		{
			return item == null || (((Entry*)item)->Header & FLAGS_DISPOSED) != 0;
		}

		internal static byte* Align(byte* ptr)
		{
			long r = ((long)ptr) & (ALIGNMENT - 1);
			if (r > 0) ptr += ALIGNMENT - r;
			return ptr;
		}

		internal static bool IsAligned(void* ptr)
		{
			return (((long)ptr) & (ALIGNMENT - 1)) == 0;
		}

		internal static int Padding(void* ptr)
		{
			return (int)(((long)ptr) & (ALIGNMENT - 1));
		}
	}

	public enum EntryType
	{
		Free = 0,
		Key = 1,
		Value = 2,
		Search = 3
	}

	internal unsafe abstract class EntryPage : IDisposable
	{
		protected byte* m_current;
		protected byte* m_start;
		protected byte* m_end;
		protected uint m_capacity;
		protected int m_count;
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

		[Conditional("DEBUG")]
		protected void CheckInvariants()
		{
			Contract.Assert(!m_handle.IsInvalid, "Memory handle should not be invalid");
			Contract.Assert(!m_handle.IsClosed, "Memory handle should not be closed");
			Contract.Ensures(Entry.IsAligned(m_current), "Current pointer should always be aligned");
			Contract.Assert(m_current <= m_start + m_capacity, "Current pointer should never be outside the page");
		}

		public int Count { get { return m_count; } }

		public long MemoryUsage { get { return (long)m_current - (long)m_start; } }

		public abstract EntryType Type { get; }

		public void Dispose()
		{
			if (!m_handle.IsClosed)
			{
				m_start = null;
				m_current = null;
				m_handle.Close();
			}
		}

		internal static byte* Align(byte* ptr, byte* end)
		{
			long r = ((long)ptr) & (Entry.ALIGNMENT - 1);
			if (r > 0) ptr += Entry.ALIGNMENT - r;
			if (ptr > end) return end;
			return ptr;
		}

		protected byte* TryAllocate(uint size)
		{
			// try to allocate an amount of memory
			// - returns null if the page is full, or too small
			// - returns a pointer to the allocated space

			byte* ptr = m_current;
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

		public void Swap(EntryPage target)
		{
			Contract.Requires(target != null);

			var old = m_handle;
			m_handle = target.m_handle;
			m_count = target.Count;
			m_capacity = target.m_capacity;
			m_start = target.m_start;
			m_end = target.m_end;
			m_current = target.m_current;
			old.Dispose();
			CheckInvariants();
		}

		[Conditional("DEBUG")]
		public abstract void Debug_Dump();
	}

	internal abstract class ElasticHeap<TPage>
		where TPage : EntryPage
	{
		protected List<TPage> m_pages = new List<TPage>();
		protected int m_gen;
		protected uint m_pageSize;
		protected TPage m_current;

		public ElasticHeap(int generation, uint pageSize)
		{
			if (generation < 0) throw new ArgumentOutOfRangeException("generation");

			m_gen = generation;
			m_pageSize = pageSize;
		}

		[Conditional("DEBUG")]
		public void Debug_Dump()
		{
			Console.WriteLine("# Dumping elastic " + typeof(TPage).Name + " heap (" + m_pages.Count + " pages)");
			foreach(var page in m_pages)
			{
				page.Debug_Dump();
			}
		}
	}

}
