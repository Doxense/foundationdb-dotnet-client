#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Utils
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.ConstrainedExecution;
	using System.Runtime.InteropServices;
	using System.Security;

	/// <summary>Slice of unmanaged memory</summary>
	[DebuggerDisplay("({Data}, {Count})"), DebuggerTypeProxy(typeof(USliceDebugView))]
	public unsafe struct USlice : IEquatable<USlice>, IComparable<USlice>
	{
		public readonly byte* Data;
		public readonly uint Count;

		/// <summary>Gets an empty slice (equivalent to the NULL pointer)</summary>
		public static USlice Nil
		{
			get { return default(USlice); }
		}

		public USlice(byte* data, uint count)
		{
			Contract.Requires(data != null || count == 0);
			this.Data = data;
			this.Count = count;
		}

		/// <summary>Checks if this is the empty slice (NULL pointer)</summary>
		public bool IsNull
		{
			get { return this.Data == null; }
		}

		public byte* AtOffset(uint offset)
		{
			if (this.Data == null || offset >= this.Count) ThrowInvalidAccess(this.Data);
			return this.Data + offset;
		}

		public byte this[uint offset]
		{
			get { return *(AtOffset(offset)); }
		}

		public USlice Substring(uint startIndex, uint count)
		{
			if (count == 0) return default(USlice);
			Contract.Requires(this.Data != null && startIndex <= this.Count && count <= this.Count && startIndex + count <= this.Count);

			if (this.Data == null) ThrowNullReference();
			if (startIndex > this.Count) ThrowIndexOutsideTheSlice();
			if (count > this.Count || startIndex + count > this.Count) ThrowSliceTooSmall();

			return new USlice(this.Data + startIndex, count);
		}

		private static void ThrowIndexOutsideTheSlice()
		{
			throw new ArgumentOutOfRangeException("Start index must be inside the slice", "startIndex");
		}

		private static void ThrowSliceTooSmall()
		{
			throw new ArgumentOutOfRangeException("Slice is too small", "count");
		}

		public IntPtr GetPointer()
		{
			return new IntPtr(this.Data);
		}

		public IntPtr GetPointer(uint offset)
		{
			return new IntPtr(AtOffset(offset));
		}

		public byte* Successor
		{
			get
			{
				if (this.Data == null) ThrowNullReference();
				return this.Data + this.Count;
			}
		}

		public byte[] GetBytes()
		{
			Contract.Requires(this.Count >= 0);
			var tmp = new byte[this.Count];
			if (this.Count > 0)
			{
				Contract.Assert(this.Data != null);
				fixed (byte* ptr = tmp)
				{
					UnmanagedHelpers.CopyUnsafe(ptr, this.Data, this.Count);
				}
			}
			return tmp;
		}

		public byte[] GetBytes(uint offset, uint count)
		{
			Contract.Requires(this.Count >= 0);

			if (offset > this.Count) throw new ArgumentOutOfRangeException("offset");
			if (offset + count >= this.Count) throw new ArgumentOutOfRangeException("count");

			var tmp = new byte[count];
			if (count > 0)
			{
				Contract.Assert(this.Data != null);
				fixed (byte* ptr = tmp)
				{
					UnmanagedHelpers.CopyUnsafe(ptr, this.Data + offset, count);
				}
			}
			return tmp;
		}

		public FoundationDB.Client.Slice ToSlice()
		{
			return FoundationDB.Client.Slice.Create(GetBytes());
		}

		public bool Equals(USlice other)
		{
			if (this.Count != other.Count) return false;
			if (this.Data == other.Data) return true;
			if (this.Data == null || other.Data == null) return false;

			//TODO: optimize!
			return 0 == UnmanagedHelpers.CompareUnsafe(this.Data, this.Count, other.Data, other.Count);
		}

		public int CompareTo(USlice other)
		{
			return UnmanagedHelpers.CompareUnsafe(this.Data, this.Count, other.Data, other.Count);
		}

		public override bool Equals(object obj)
		{
			if (obj == null) return this.Data == null && this.Count == 0;
			return obj is USlice && Equals((USlice)obj);
		}

		public override int GetHashCode()
		{
			return UnmanagedHelpers.ComputeHashCode(ref this);
		}

		public override string ToString()
		{
			return "{" + (long)this.Data + ", " + this.Count + "}";
		}

		private static void ThrowNullReference()
		{
			throw new InvalidOperationException("Cannot access NULL pointer");
		}

		private static void ThrowInvalidAccess(byte* ptr)
		{
			if (ptr == null) ThrowNullReference();
			throw new IndexOutOfRangeException();
		
		}

		private sealed class USliceDebugView
		{
			private readonly USlice m_slice;

			public USliceDebugView(USlice slice)
			{
				m_slice = slice;
			}

			public uint Size
			{
				get { return m_slice.Count; }
			}

			public byte[] Data
			{
				get { return m_slice.GetBytes(); }
			}

		}

	}

}
