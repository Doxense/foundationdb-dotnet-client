#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Client;
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;
	using System.Threading;

	/// <summary>Slice builder backed by a buffer stored in unmanaged memory</summary>
	[DebuggerDisplay("Count={m_count}, Capacity={m_capacity}"), DebuggerTypeProxy(typeof(UnmanagedSliceBuilder.DebugView))]
	public unsafe sealed class UnmanagedSliceBuilder : IDisposable
	{
		//TODO: define a good default value for this.
		public const uint DEFAULT_CAPACITY = 1024;

		private byte* m_data;
		private uint m_count;
		private uint m_capacity;
		private IntPtr m_handle;
		private bool m_disposed;

		#region Constuctors...

		public UnmanagedSliceBuilder()
			: this(0)
		{ }

		public UnmanagedSliceBuilder(uint capacity)
		{
			if (capacity > 0)
			{
				GrowBuffer(capacity);
			}
		}

		public UnmanagedSliceBuilder(USlice slice)
			: this(slice.Data, slice.Count)
		{ }

		public UnmanagedSliceBuilder(Slice slice)
		{
			if (slice.Count < 0 || slice.Offset < 0) ThrowMalformedManagedSlice();

			uint size = (uint)slice.Count;
			if (size > 0)
			{
				if (slice.Array == null || slice.Array.Length < slice.Offset + slice.Count) ThrowMalformedManagedSlice();
				GrowBuffer(size);

				fixed (byte* ptr = slice.Array)
				{
					UnmanagedHelpers.CopyUnsafe(this.Data, ptr + slice.Offset, size);
				}
				m_count = size;
			}
		}

		private static void ThrowMalformedManagedSlice()
		{
			throw new ArgumentException("Malformed slice", "slice");
		}

		public UnmanagedSliceBuilder(byte* data, uint size)
		{
			Contract.Requires(size == 0 || data != null);
			if (size > 0)
			{
				GrowBuffer(size);
				UnmanagedHelpers.CopyUnsafe(this.Data + this.Count, data, size);
				m_count = size;
			}
		}

		~UnmanagedSliceBuilder()
		{
			Dispose(false);
		}

		#endregion

		#region Public Properties...

		/// <summary>Gets a handle on the memory allocated for this buffer</summary>
		internal IntPtr Buffer
		{
			get { return m_handle; }
		}

		/// <summary>Gets a pointer to the first byte in the buffer</summary>
		public byte* Data
		{
			get { return m_data; }
		}

		/// <summary>Gets the number of bytes written to the buffer</summary>
		public uint Count
		{
			get { return m_count; }
		}

		/// <summary>Checks if the builder is empty.</summary>
		public bool Empty
		{
			get { return m_count == 0; }
		}

		/// <summary>Gets the current capacity of the buffer</summary>
		public uint Capacity
		{
			get { return m_capacity; }
		}

		/// <summary>Gets or sets the byte at the specified offset</summary>
		/// <param name="offset">Offset from the start of the buffer (0-based)</param>
		/// <returns>Value of the byte at this offset</returns>
		/// <exception cref="System.IndexOutOfRangeException">if <paramref name="offset"/> is outside the current size of the buffer</exception>
		public byte this[uint offset]
		{
			get
			{
				if (offset >= m_count) ThrowIndexOutOfRange();
				return this.Data[offset];
			}
			set
			{
				if (offset >= m_count) ThrowIndexOutOfRange();
				this.Data[offset] = value;
			}
		}

		#endregion

		/// <summary>Grow the buffer to be able to hold the specified number of bytes</summary>
		/// <param name="required">Minimum capacity required</param>
		/// <remarks>The buffer may be resize to more than <paramref name="required"/></remarks>
		private void GrowBuffer(uint required)
		{
			if (m_handle == IntPtr.Zero)
			{
				uint newsize = UnmanagedHelpers.NextPowerOfTwo(Math.Max(required, DEFAULT_CAPACITY));
				var buf = Marshal.AllocHGlobal(new IntPtr(newsize));
				if (buf == IntPtr.Zero) throw new OutOfMemoryException();
				m_handle = buf;
				m_data = (byte*)buf.ToPointer();
				m_count = 0;
				m_capacity = newsize;
			}
			else
			{
				uint newsize = UnmanagedHelpers.NextPowerOfTwo(Math.Max(required, m_capacity << 1));
				var buf = Marshal.ReAllocHGlobal(m_handle, new IntPtr(newsize));
				if (m_handle == IntPtr.Zero) throw new OutOfMemoryException();
				m_handle = buf;
				m_data = (byte*)buf.ToPointer();
				m_capacity = newsize;
			}
		}

		public void Clear()
		{
			if (m_disposed) ThrowAlreadyDisposed();
			m_count = 0;
		}

		private byte* AllocateInternal(uint size, bool zeroed)
		{
			if (m_disposed) ThrowAlreadyDisposed();
			Contract.Requires(size != 0);

			uint remaining = m_capacity - m_count;
			if (remaining < size)
			{
				GrowBuffer(m_count + size);
			}

			uint pos = m_count;
			m_count = pos + size;
			byte* ptr = this.Data + pos;
			if (zeroed) UnmanagedHelpers.FillUnsafe(ptr, size, 0);
			return ptr;
		}

		public USlice Allocate(uint size, bool zeroed = false)
		{
			if (size == 0) return default(USlice);
			return new USlice(AllocateInternal(size, zeroed), size);
		}

		public void Append(byte* source, uint size)
		{
			if (size == 0) return;
			if (source == null) ThrowInvalidSource();

			byte* ptr = AllocateInternal(size, zeroed: false);
			Contract.Assert(ptr != null);
			UnmanagedHelpers.CopyUnsafe(ptr, source, size);
		}

		public void Append(USlice source)
		{
			if (source.Count == 0) return;
			if (source.Data == null) ThrowInvalidSource();

			byte* ptr = AllocateInternal(source.Count, zeroed: false);
			Contract.Assert(ptr != null);
			UnmanagedHelpers.CopyUnsafe(ptr, source);
		}

		public void Append(Slice source)
		{
			if (source.Count == 0) return;
			if (source.Array == null || source.Offset < 0 || source.Count < 0) ThrowInvalidSource();

			var ptr = AllocateInternal((uint)source.Count, zeroed: false);
			Contract.Assert(ptr != null);
			UnmanagedHelpers.CopyUnsafe(ptr, source);
		}

		public void Set(USlice source)
		{
			m_count = 0;
			if (source.Count > 0)
			{
				if (source.Data == null) ThrowInvalidSource();

				var ptr = AllocateInternal(source.Count, zeroed: false);
				Contract.Assert(ptr != null);
				UnmanagedHelpers.CopyUnsafe(ptr, source);
			}
		}

		public void Set(Slice source)
		{
			m_count = 0;
			if (source.Count > 0)
			{
				if (source.Array == null || source.Offset < 0) ThrowInvalidSource();

				var ptr = AllocateInternal((uint)source.Count, zeroed: false);
				Contract.Assert(ptr != null);
				UnmanagedHelpers.CopyUnsafe(ptr, source);
			}
		}

		public void Resize(uint newSize, byte filler)
		{
			if (newSize <= m_count)
			{
				m_count = newSize;
			}
			else
			{
				if (newSize > m_capacity) GrowBuffer(newSize);

				// fill the extra space with zeroes
				uint pos = m_count;
				uint r = m_capacity - newSize;
				if (r > 0)
				{
					UnmanagedHelpers.FillUnsafe(this.Data + pos, r, 0);
				}
				m_count = newSize;
			}
		}

		public void Swap(UnmanagedSliceBuilder other)
		{
			if (other == null) throw new ArgumentNullException("other");
			if (m_disposed || other.m_disposed) ThrowAlreadyDisposed();

			var buf = other.m_handle;
			var sz = other.m_count;
			var cap = other.m_capacity;

			other.m_handle = m_handle;
			other.m_data = (byte*)buf.ToPointer();
			other.m_count = m_count;
			other.m_capacity = m_capacity;

			m_handle = buf;
			m_data = (byte*)buf.ToPointer();
			m_count = sz;
			m_capacity = cap;
		}

		/// <summary>Gets the current content of the buffer as an unmanaged slice</summary>
		/// <returns>Slice that points the content of the buffer.</returns>
		/// <remarks>Caution: do NOT use the returned slice after the buffer has been changed (it can get relocated during a resize)</remarks>
		public USlice ToUSlice()
		{
			if (m_disposed) ThrowAlreadyDisposed();

			if (this.Count == 0) return default(USlice);
			return new USlice(this.Data, this.Count);
		}

		internal USlice CopyTo(byte* dest)
		{
			if (m_disposed) ThrowAlreadyDisposed();

			if (m_count > 0)
			{
				UnmanagedHelpers.CopyUnsafe(dest, this.Data, m_count);
				return new USlice(dest, m_count);
			}
			return default(USlice);
		}

		public byte[] GetBytes()
		{
			if (m_disposed) ThrowAlreadyDisposed();

			var tmp = new byte[m_count];
			if (this.Count >= 0)
			{
				fixed (byte* ptr = tmp)
				{
					UnmanagedHelpers.CopyUnsafe(ptr, this.Data, m_count);
				}
			}
			return tmp;
		}

		private static void ThrowIndexOutOfRange()
		{
			throw new IndexOutOfRangeException();
		}

		private void ThrowAlreadyDisposed()
		{
			throw new ObjectDisposedException(this.GetType().Name);
		}

		private void ThrowInvalidSource()
		{
			throw new ArgumentException("The source memory location is invalid");
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (m_handle != IntPtr.Zero)
				{
					Marshal.FreeHGlobal(m_handle);
				}
			}
			m_handle = IntPtr.Zero;
			m_data = null;
			m_count = 0;
			m_capacity = 0;
		}

		private sealed class DebugView
		{
			private readonly UnmanagedSliceBuilder m_builder;

			public DebugView(UnmanagedSliceBuilder builder)
			{
				m_builder = builder;
			}

			public byte[] Data
			{
				get
				{
					if (m_builder.m_disposed || m_builder.m_count == 0) return null;
					return m_builder.GetBytes();
				}
			}

			public uint Count
			{
				get { return m_builder.m_count; }
			}

			public uint Capacity
			{
				get { return m_builder.m_capacity; }
			}

		}

	}

}
