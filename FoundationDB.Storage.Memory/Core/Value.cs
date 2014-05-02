#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal unsafe struct Value
	{

		// A Value contains the pointer to the key's bytes, and a pointer to the most current Value for this key, or null if the key is currently deleted

		//	Field	Offset	Bits	Type		Desc
		//  HEADER		0	 16		flags		Type, status flags, deletion or mutation flags, ....
		//  reserved	2	 16		uint16		unused
		//  SIZE		4	 32		uint		Size of the DATA field (can be 0, should only use 24 bits at most)
		//  SEQUENCE	8	 64		ulong		Sequence version of this value
		//	PREVIOUS   16	 64		Value*		Pointer to the previous value that was supersed by this entry (or null if we are the oldest one in the chain)
		//	PARENT	   24	 64		void*		Pointer to the parent of this value
		//	DATA	   32	 ..		byte[]		First byte of the key

		// The HEADER flags are as follow:
		// - bit 0: DELETION					If set, this value is a deletion marker (and its size must be zero)
		// - bit 1: MUTATED						If set, this value is not the last one for this key
		// - bit 2-5: unused
		// - bit 7: HAS_WATCH					If set, this key is currently being watched
		// - bit 8-15: ENTRY_FLAGS				(inherited from Entry)
		// - bit 8-15: ENTRY_FLAGS				(inherited from Entry)

		public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Value), "Data").ToInt32();

		/// <summary>This value is a deletion marker</summary>
		public const ushort FLAGS_DELETION = 1 << 0;

		/// <summary>This value has been mutated and is not up to date</summary>
		public const ushort FLAGS_MUTATED = 1 << 1;

		/// <summary>Various flags (TDB)</summary>
		public ushort Header;
		/// <summary>Not used</summary>
		public uint Reseved;
		/// <summary>Size of the value</summary>
		public uint Size;
		/// <summary>Version where this version of the key first appeared</summary>
		public ulong Sequence;
		/// <summary>Pointer to the previous version of this key, or NULL if this is the earliest known</summary>
		public Value* Previous;
		/// <summary>Pointer to the parent node (can be a Key or a Value)</summary>
		public void* Parent;
		/// <summary>Offset to the first byte of the value</summary>
		public byte Data;

		public static USlice GetData(Value* value)
		{
			if (value == null) return default(USlice);

			Contract.Assert((value->Header & Entry.FLAGS_DISPOSED) == 0, "Attempt to read a value that was disposed");
			return new USlice(&(value->Data), value->Size);
		}

		public static bool StillAlive(Value* value, ulong sequence)
		{
			if (value == null) return false;
			if ((value->Header & Value.FLAGS_MUTATED) != 0)
			{
				return value->Sequence >= sequence;
			}
			return true;
		}

		public static bool IsDisposed(Value* value)
		{
			return (value->Header & Entry.FLAGS_DISPOSED) != 0;
		}

		/// <summary>Return the address of the following value in the heap</summary>
		internal static Value* WalkNext(Value* self)
		{
			Contract.Requires(self != null && Entry.GetObjectType(self) == EntryType.Value);

			return (Value*)Entry.Align((byte*)self + Value.SizeOf + self->Size);
		}

	}

}
