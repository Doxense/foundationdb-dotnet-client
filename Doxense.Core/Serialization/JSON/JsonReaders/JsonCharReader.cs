#region Copyright (c) 2005-2023 Doxense SAS
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

	/// <summary>JSON Text reader that wraps an in-memory buffer of characters</summary>
	[DebuggerDisplay("Remaining={Remaining}")]
	public unsafe struct JsonCharReader : IJsonReader
	{
		private char* Cursor;
		private readonly char* End;

		/// <summary>Create a new char reader from an unmanaged memory buffer</summary>
		/// <param name="buffer">Buffer containing decoded characters</param>
		/// <param name="autoDetectBom">If true, skip the BOM if found ('\xFEFF')</param>
		public JsonCharReader(char* buffer, int count, bool autoDetectBom = true)
		{
			this.Cursor = buffer + (autoDetectBom && count >= 1 && buffer[0] == 0xFEFF ? 1 : 0);
			this.End = buffer + count;
		}

		public int Read()
		{
			var cursor = this.Cursor;
			if (cursor >= this.End)
			{
				return -1;
			}
			this.Cursor = cursor + 1;
			return *cursor;
		}

		public bool HasMore => this.Cursor < this.End;

		public int Remaining => this.Cursor < this.End ? (int) (this.End - this.Cursor) : 0;

	}
}
