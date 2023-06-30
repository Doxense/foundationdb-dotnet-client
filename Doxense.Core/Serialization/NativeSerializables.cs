#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization
{
	using System;
	using System.Runtime.CompilerServices;
	using Doxense.Memory;

	public readonly struct Utf8StringValue : ISliceSerializable
	{
		public readonly string? Value;

		public Utf8StringValue(string? value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteStringUtf8(this.Value);

		public static implicit operator Utf8StringValue(string? value) => new Utf8StringValue(value);
	}

	public readonly struct Int32Value : ISliceSerializable
	{
		public readonly int Value;

		public Int32Value(int value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteFixed32(this.Value);

		public static implicit operator Int32Value(int value) => new Int32Value(value);
	}

	public readonly struct UInt32Value : ISliceSerializable
	{
		public readonly uint Value;

		public UInt32Value(uint value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteFixed32(this.Value);

		public static implicit operator UInt32Value(uint value) => new UInt32Value(value);
	}

	public readonly struct Int64Value : ISliceSerializable
	{
		public readonly long Value;

		public Int64Value(long value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteFixed64(this.Value);

		public static implicit operator Int64Value(long value) => new Int64Value(value);
	}

	public readonly struct UInt64Value : ISliceSerializable
	{
		public readonly ulong Value;

		public UInt64Value(ulong value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteFixed64(this.Value);

		public static implicit operator UInt64Value(ulong value) => new UInt64Value(value);
	}

}
