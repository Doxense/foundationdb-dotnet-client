#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Diagnostics;
	using System.Runtime.InteropServices;

	public enum EntryType
	{
		Free = 0,
		Key = 1,
		Value = 2,
		Search = 3
	}

	[DebuggerDisplay("Header={Header}, Size={Size}")]
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

}
