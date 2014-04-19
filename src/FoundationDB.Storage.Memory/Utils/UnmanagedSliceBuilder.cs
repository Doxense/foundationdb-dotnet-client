#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Utils
{
	using FoundationDB.Client;
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;

	/// <summary>Unmanaged slice builder backed by a pinned managed buffer</summary>
	/// <remarks>This class is not thread-safe.</remarks>
	[DebuggerDisplay("Count={m_count}, Capacity={m_capacity}"), DebuggerTypeProxy(typeof(UnmanagedSliceBuilder.DebugView))]
	public unsafe sealed class UnmanagedSliceBuilder : IDisposable
	{
		private static readonly byte[] s_empty = new byte[0];

		//TODO: define a good default value for this.
		public const uint DEFAULT_CAPACITY = 1024;

		/// <summary>Managed buffer used to store the values</summary>
		private byte[] m_buffer;
		/// <summary>Pinned address of the buffer</summary>
		private byte* m_data;
		/// <summary>Number of bytes currently written to the buffer</summary>
		private uint m_count;
		/// <summary>GC handle used to pin the managed buffer</summary>
		private GCHandle m_handle;

		#region Constuctors...

		public UnmanagedSliceBuilder()
			: this(0)
		{ }

		public UnmanagedSliceBuilder(uint capacity)
		{
			if (capacity == 0)
			{
				m_buffer = s_empty;
			}
			else
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
			if (data == null && size != 0) throw new ArgumentNullException("data");
			if (size == 0)
			{
				m_buffer = s_empty;
			}
			else
			{
				GrowBuffer(size);
				UnmanagedHelpers.CopyUnsafe(m_data, data, size);
				m_count = size;
			}
		}

		~UnmanagedSliceBuilder()
		{
			Dispose(false);
		}

		#endregion

		#region Public Properties...

		/// <summary>Gets the managed buffer</summary>
		public byte[] Buffer
		{
			get { return m_buffer; }
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
			get { return m_buffer == null ? 0U : (uint)m_buffer.Length; }
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
			try
			{ }
			finally
			{
				if (!m_handle.IsAllocated)
				{ // initial allocation of the buffer
					uint newsize = UnmanagedHelpers.NextPowerOfTwo(Math.Max(required, DEFAULT_CAPACITY));
					var buffer = new byte[newsize];
					m_buffer = buffer;
					m_count = 0;
				}
				else
				{ // resize an existing buffer
					uint newsize = (uint)m_buffer.Length;
					newsize = UnmanagedHelpers.NextPowerOfTwo(Math.Max(required, newsize << 1));
					if (newsize > int.MaxValue)
					{ // cannot alloc more than 2GB in managed code! 
						newsize = int.MaxValue;
						if (newsize < required) throw new OutOfMemoryException("Cannot grow slice builder above 2GB");
					}
					// temporary release the handle
					m_data = null;
					m_handle.Free();
					// resize to the new capacity, and re-pin
					Array.Resize(ref m_buffer, (int)newsize);
				}
				m_handle = GCHandle.Alloc(m_buffer, GCHandleType.Pinned);
				m_data = (byte*)m_handle.AddrOfPinnedObject().ToPointer();
			}
			Contract.Ensures(m_buffer != null && m_handle.IsAllocated && m_data != null && m_count >= 0 && m_count <= m_buffer.Length, "GrowBuffer corruption");
		}

		public void Clear()
		{
			if (m_buffer == null) ThrowAlreadyDisposed();
			m_count = 0;
		}

		private byte* AllocateInternal(uint size, bool zeroed)
		{
			if (m_buffer == null) ThrowAlreadyDisposed();
			Contract.Requires(size != 0, "size == 0");

			Contract.Assert(m_buffer != null && m_count <= m_buffer.Length, "Builder is corrupted");
			uint remaining = checked(((uint)m_buffer.Length) - m_count);
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
			Contract.Assert(ptr != null, "AllocateInternal() => null");
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
			if (source.Count > 0)
			{
				if (source.Array == null || source.Offset < 0) ThrowInvalidSource();

				var ptr = AllocateInternal((uint)source.Count, zeroed: false);
				Contract.Assert(ptr != null, "AllocateInternal() => null");
				UnmanagedHelpers.CopyUnsafe(ptr, source);
			}
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
			if (m_buffer == null) ThrowAlreadyDisposed();
			if (newSize <= m_count)
			{
				m_count = newSize;
			}
			else
			{
				if (newSize > m_buffer.Length) GrowBuffer(newSize);

				// fill the extra space with zeroes
				uint pos = m_count;
				uint r = checked((uint)m_buffer.Length - newSize);
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
			if (m_buffer == null || other.m_buffer == null) ThrowAlreadyDisposed();

			try
			{ }
			finally
			{
				var handle = other.m_handle;
				var buffer = other.m_buffer;
				var data = other.m_data;
				var sz = other.m_count;

				other.m_handle = m_handle;
				other.m_buffer = buffer;
				other.m_data = m_data;
				other.m_count = m_count;

				m_handle = handle;
				m_buffer = buffer;
				m_data = data;
				m_count = sz;
			}
		}

		/// <summary>Gets the current content of the buffer as a managed slice</summary>
		/// <returns>Slice that points to the content of the buffer.</returns>
		/// <remarks>Caution: do NOT use the returned slice after the buffer has been changed (it can get relocated during a resize)</remarks>
		public Slice ToSlice()
		{
			if (m_buffer == null) ThrowAlreadyDisposed();
			return m_count > 0 ? Slice.Create(m_buffer, 0, (int)m_count) : default(Slice);
		}

		/// <summary>Gets the current content of the buffer as an unmanaged slice</summary>
		/// <returns>Slice that points to the content of the buffer.</returns>
		/// <remarks>Caution: do NOT use the returned slice after the buffer has been changed (it can get relocated during a resize)</remarks>
		public USlice ToUSlice()
		{
			if (m_buffer == null) ThrowAlreadyDisposed();
			return m_count > 0 ? new USlice(m_data, m_count) : default(USlice);
		}

		/// <summary>Gets the a segment of the buffer as an unmanaged slice</summary>
		/// <param name="count">Number of bytes (from the start) to return</param>
		/// <returns>Slice that points to the specified segment of the buffer.</returns>
		/// <remarks>Caution: do NOT use the returned slice after the buffer has been changed (it can get relocated during a resize)</remarks>
		public USlice ToUSlice(uint count)
		{
			return ToUSlice(0, count);
		}

		/// <summary>Gets the a segment of the buffer as an unmanaged slice</summary>
		/// <param name="offset">Offset from the start of the buffer</param>
		/// <param name="count">Number of bytes to return</param>
		/// <returns>Slice that points to the specified segment of the buffer.</returns>
		/// <remarks>Caution: do NOT use the returned slice after the buffer has been changed (it can get relocated during a resize)</remarks>
		public USlice ToUSlice(uint offset, uint count)
		{
			if (m_buffer == null) ThrowAlreadyDisposed();
			if (offset > m_count) throw new ArgumentOutOfRangeException("offset");
			if (count == 0) return default(USlice);
			if (offset + count > m_count) throw new ArgumentOutOfRangeException("count");

			return new USlice(m_data + offset, count);
		}

		/// <summary>Copy the content of the buffer to an unmanaged pointer, and return the corresponding slice</summary>
		/// <param name="dest">Destination pointer where the buffer will be copied. Caution: the destination buffer must be large enough!</param>
		/// <returns>Slice that points to the copied segment in the destination buffer</returns>
		internal USlice CopyTo(byte* dest)
		{
			return CopyTo(dest, m_count);
		}

		/// <summary>Copy a segment of the buffer to an unmanaged pointer, and return the corresponding slice</summary>
		/// <param name="count">Number of bytes to copy</param>
		/// <param name="dest">Destination pointer where the buffer will be copied. Caution: the destination buffer must be large enough!</param>
		/// <returns>Slice that points to the copied segment in the destination buffer</returns>
		internal USlice CopyTo(byte* dest, uint count)
		{
			if (m_buffer == null) ThrowAlreadyDisposed();
			if (count == 0) return default(USlice);
			if (count > m_count) throw new ArgumentOutOfRangeException("count");

			UnmanagedHelpers.CopyUnsafe(dest, m_data, count);
			return new USlice(dest, count);
		}

		public byte[] GetBytes()
		{
			if (m_buffer == null) ThrowAlreadyDisposed();

			var tmp = new byte[m_count];
			if (m_count >= 0)
			{
				fixed (byte* ptr = tmp)
				{
					UnmanagedHelpers.CopyUnsafe(ptr, m_data, m_count);
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
			if (disposing)
			{
				if (m_handle.IsAllocated)
				{
					m_handle.Free();
				}

			}
			m_data = null;
			m_buffer = null;
			m_count = 0;
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
					if (m_builder.m_count == 0) return s_empty;
					var buffer = m_builder.m_buffer;
					if (buffer == null) return null;
					var tmp = new byte[m_builder.Count];
					System.Buffer.BlockCopy(m_builder.m_buffer, 0, tmp, 0, tmp.Length);
					return tmp;
				}
			}

			public uint Count
			{
				get { return m_builder.m_count; }
			}

			public uint Capacity
			{
				get { return m_builder.m_buffer == null ? 0U : (uint)m_builder.m_buffer.Length; }
			}

		}

	}

}
