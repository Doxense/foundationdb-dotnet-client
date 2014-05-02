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
	internal unsafe struct Key
	{
		// A Key contains the key's bytes, an hashcode, and a pointer to the most current Value for this key, or null if the key is currently deleted

		//	Field	Offset	Bits	Type		Desc
		//  HEADER		0	 16		flags		Type, status flags, deletion or mutation flags, ....
		//  SIZE		2	 16		uint16		Size of the DATA field (from 0 to 10,000). Note: bit 14 and 15 are usually 0 and could be used for something?)
		//  HASHCODE	4	 32		uint32		Hashcode (note: size only need 2 bytes, so maybe we could extand this to 24 bits?)
		//	VALUEPTR	8	 64		Value*		Pointer to the most current value of this key (or null if the DELETION bit is set in the header)
		//	DATA	   16	 ..		byte[]		First byte of the key

		// The HEADER flags are as follow:
		// - bit 0: NEW							If set, this key has been inserted after the last GC
		// - bit 1: MUTATED						If set, this key has changed aster the last GC
		// - bit 2-5: unused
		// - bit 7: HAS_WATCH					If set, this key is currently being watched
		// - bit 8-15: ENTRY_FLAGS				(inherited from Entry)

		public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Key), "Data").ToInt32();

		/// <summary>The key has been inserted after the last GC</summary>
		public const ushort FLAGS_NEW = 1 << 0;
		/// <summary>The key has been created/mutated since the last GC</summary>
		public const ushort FLAGS_MUTATED = 1 << 1;
		/// <summary>There is a watch listening on this key</summary>
		public const ushort FLAGS_HAS_WATCH = 1 << 7;

		/// <summary>Various flags (TODO: enum?)</summary>
		public ushort Header;
		/// <summary>Size of the key (in bytes)</summary>
		public ushort Size;
		/// <summary>Hashcode of the key</summary>
		public int HashCode;
		/// <summary>Pointer to the head of the value chain for this key (should not be null)</summary>
		public Value* Values;
		/// <summary>Offset to the first byte of the key</summary>
		public byte Data;

		public static USlice GetData(Key* self)
		{
			if (self == null) return default(USlice);
			Contract.Assert((self->Header & Entry.FLAGS_DISPOSED) == 0, "Attempt to read a key that was disposed");
			return new USlice(&(self->Data), self->Size);
		}

		public static bool StillAlive(Key* self, ulong sequence)
		{
			if (self == null) return false;

			if ((self->Header & Entry.FLAGS_UNREACHABLE) != 0)
			{ // we have been marked as dead

				var value = self->Values;
				if (value == null) return false;

				// check if the last value is a deletion?
				if (value->Sequence <= sequence && (value->Header & Value.FLAGS_DELETION) != 0)
				{ // it is deleted
					return false;
				}
			}

			return true;
		}

		public static bool IsDisposed(Key* self)
		{
			return (self->Header & Entry.FLAGS_DISPOSED) != 0;
		}

		/// <summary>Return the address of the following value in the heap</summary>
		internal static Key* WalkNext(Key* self)
		{
			Contract.Requires(self != null && Entry.GetObjectType(self) == EntryType.Key);

			return (Key*)Entry.Align((byte*)self + Key.SizeOf + self->Size);
		}

	}

}
