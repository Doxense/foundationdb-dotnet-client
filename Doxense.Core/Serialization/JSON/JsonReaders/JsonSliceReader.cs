#region Copyright Doxense 2010-2021
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

	/// <summary>JSON text reader that reads UTF-8 encoded bytes from an in-memory buffer</summary>
	[DebuggerDisplay("Remaining={" + nameof(Remaining) + "}")]
	public struct JsonSliceReader : IJsonReader
	{
		/// <summary>Current position, in <see cref="Array"/></summary>
		private int Cursor;

		/// <summary>End offset </summary>
		private readonly int End;

		/// <summary>Buffer containing the UTF-8 encoded string</summary>
		private readonly byte[] Array;

		/// <summary>Create a new UTF-8 reader from an unmanaged memory buffer</summary>
		/// <param name="buffer">Buffer containing UTF-8 encoded bytes</param>
		/// <param name="autoDetectBom">If true, skip the UTF-8 BOM if found (EF BB BF)</param>
		public JsonSliceReader(Slice buffer, bool autoDetectBom = true)
		{
			this.Cursor = buffer.Offset + (autoDetectBom && buffer.Count >= 3 && (buffer[0] == 0xEF & buffer[1] == 0xBB & buffer[2] == 0xBF) ? 3 : 0);
			this.End = buffer.Offset + buffer.Count;
			this.Array = buffer.Array;
		}

		public int Read()
		{
			int cursor = this.Cursor;
			if (cursor < this.End)
			{
				byte c = this.Array[cursor];
				if (c < 0x80)
				{
					// ASCII character
					this.Cursor = cursor + 1;
					return c;
				}
			}
			return ReadSlow();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int ReadSlow()
		{
			int index = this.Cursor;
			int end = this.End;
			if (index >= end)
			{ // EOF
				this.Cursor = end;
				return -1;
			}

			if (!Utf8Encoder.TryDecodeCodePoint(this.Array, index, end, out var cp, out var len))
			{
				throw new InvalidDataException("Buffer contains malformed UTF-8 characters.");
			}
			this.Cursor = index + len;
			return (int) cp;

		}

		/// <summary>Returns true if there are more characters to read</summary>
		public bool HasMore => this.Cursor < this.End;

		public int Remaining => this.Cursor < this.End ? (int) (this.End - this.Cursor) : 0;

	}

}
