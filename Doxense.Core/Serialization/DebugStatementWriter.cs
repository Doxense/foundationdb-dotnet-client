#region Copyright Doxense 2012-2020
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization
{
	using JetBrains.Annotations;
	using System;
	using System.Globalization;
	using System.Text;

	/// <summary>Very simple writer to dump query expressions into a statement useful for logging/debugging</summary>
	public sealed class DebugStatementWriter
	{
		public DebugStatementWriter()
		{
			this.Buffer = new StringBuilder();
			this.StartOfLine = true;
		}

		public StringBuilder Buffer { get; }
		public int IndentLevel { get; private set; }
		public bool StartOfLine { get; private set; }

		public DebugStatementWriter Enter()
		{
			this.IndentLevel++;
			if (!this.StartOfLine) WriteLine();
			return this;
		}

		public DebugStatementWriter Leave()
		{
			this.IndentLevel--;
			if (!this.StartOfLine) WriteLine();
			return this;
		}

		public DebugStatementWriter WriteLine(string text)
		{
			if (this.StartOfLine) Indent();
			this.Buffer.AppendLine(text);
			this.StartOfLine = true;
			return this;
		}

		public DebugStatementWriter WriteLine()
		{
			return WriteLine(string.Empty);
		}

		public DebugStatementWriter Write(string text)
		{
			if (this.StartOfLine) Indent();
			this.Buffer.Append(text);

			return this;
		}

		[StringFormatMethod("format")]
		public DebugStatementWriter WriteLine(string format, params object?[] args)
		{
			return WriteLine(string.Format(CultureInfo.InvariantCulture, format, args));
		}

		[StringFormatMethod("format")]
		public DebugStatementWriter Write(string format, params object?[] args)
		{
			return Write(string.Format(CultureInfo.InvariantCulture, format, args));
		}

		private void Indent()
		{
			switch (this.IndentLevel)
			{
				case 0: break;
				case 1: this.Buffer.Append('\t'); break;
				case 2: this.Buffer.Append("\t\t"); break;
				case 3: this.Buffer.Append("\t\t\t"); break;
				default: this.Buffer.Append(new string('\t', this.IndentLevel)); break;
			}
			this.StartOfLine = false;
		}

		public override string ToString()
		{
			return this.Buffer.ToString();
		}
	}

}
