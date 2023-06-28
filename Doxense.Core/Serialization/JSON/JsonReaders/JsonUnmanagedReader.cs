#region Copyright Doxense 2010-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime.CompilerServices;
	using Doxense.Text;

	/// <summary>JSON text reader that reads from UTF-8 encoded bytes in native memory</summary>
	[DebuggerDisplay("Remaining={Remaining}")]
	public unsafe struct JsonUnmanagedReader : IJsonReader
	{
		private byte* Cursor;
		private readonly byte* End;

		/// <summary>Create a new UTF-8 reader from an unmanaged memory buffer</summary>
		/// <param name="buffer">Buffer containing UTF-8 encoded bytes</param>
		/// <param name="length">Length of the buffer (in bytes)</param>
		/// <param name="autoDetectBom">If true, skip the UTF-8 BOM if found (EF BB BF)</param>
		public JsonUnmanagedReader(byte* buffer, int length, bool autoDetectBom = true)
		{
			this.Cursor = buffer + ((autoDetectBom && length >= 3 && (buffer[0] == 0xEF & buffer[1] == 0xBB & buffer[2] == 0xBF)) ? 3 : 0);
			this.End = buffer + length;
		}

		public int Read()
		{
			var cursor = this.Cursor;
			if (cursor < this.End)
			{
				byte c = *cursor;
				if (c < 0x80)
				{ // ASCII character
					this.Cursor = cursor + 1;
					return c;
				}
			}
			return ReadSlow();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int ReadSlow()
		{
			var cursor = this.Cursor;
			if (cursor >= this.End)
			{ // EOF
				return -1;
			}

			//TODO: PERF: we already know the first byte is >= 0x80, so maybe we can optimize the decoding ?
			if (!Utf8Encoder.TryDecodeCodePoint(cursor, this.End, out UnicodeCodePoint cp, out int len))
			{
				throw new InvalidDataException("Buffer contains malformed UTF-8 character");
			}
			this.Cursor = cursor + len;
			return (int) cp;

		}

		public bool HasMore => this.Cursor < this.End;

		public int Remaining => this.Cursor < this.End ? (int) (this.End - this.Cursor) : 0;
	}
}
