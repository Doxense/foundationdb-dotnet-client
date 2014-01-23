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
		public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Value), "Data").ToInt32();

		/// <summary>This value is a deletion marker</summary>
		public const uint FLAGS_DELETION = 0x1;
		/// <summary>This value has been mutated and is not up to date</summary>
		public const uint FLAGS_MUTATED = 0x2;

		/// <summary>Various flags (TDB)</summary>
		public uint Header;
		/// <summary>Size of the value</summary>
		public uint Size;
		/// <summary>Version were this version of the key first appeared</summary>
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
