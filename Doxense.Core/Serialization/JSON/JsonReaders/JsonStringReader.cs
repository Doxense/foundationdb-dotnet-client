#region Copyright Doxense 2010-2018
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

	/// <summary>JSON Text reader that wraps a string</summary>
	[DebuggerDisplay("Pos={Pos}, Length={Text.Length}")]
	public struct JsonStringReader : IJsonReader
	{
		public readonly string Text;
		public int Pos;

		public JsonStringReader(string? text)
		{
			this.Text = text ?? string.Empty;
			this.Pos = 0;
		}

		public int Read()
		{
			int pos = this.Pos;
			var text = this.Text;
			if (pos >= text.Length) return -1;
			char c = text[pos];
			this.Pos = pos + 1;
			return c;
		}

		public bool HasMore => this.Pos < this.Text.Length;

	}
}
