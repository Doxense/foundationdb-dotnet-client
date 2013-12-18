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
		public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Key), "Data").ToInt32();

		/// <summary>The key has been inserted after the last GC</summary>
		public const uint FLAGS_NEW = 0x1000;
		/// <summary>The key has been created/mutated since the last GC</summary>
		public const uint FLAGS_MUTATED = 0x2000;
		/// <summary>There is a watch listening on this key</summary>
		public const uint FLAGS_HAS_WATCH = 0x2000;

		/// <summary>Various flags (TODO: enum?)</summary>
		public uint Header;
		/// <summary>Size of the key (in bytes)</summary>
		public uint Size;
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
