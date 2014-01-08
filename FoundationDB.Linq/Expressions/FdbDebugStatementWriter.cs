#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Linq.Expressions
{
	using System;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Text;

	/// <summary>Very simple writer to dump query expressions into a statement usefull for logging/debugging</summary>
	public sealed class FdbDebugStatementWriter
	{
		public FdbDebugStatementWriter()
		{
			this.Buffer = new StringBuilder();
			this.StartOfLine = true;
		}

		public StringBuilder Buffer { get; private set; }
		public int IndentLevel { get; private set; }
		public bool StartOfLine {get; private set;}

		public FdbDebugStatementWriter Enter()
		{
			this.IndentLevel++;
			if (!this.StartOfLine) WriteLine();
			return this;
		}

		public FdbDebugStatementWriter Leave()
		{
			this.IndentLevel--;
			if (!this.StartOfLine) WriteLine();
			return this;
		}

		public FdbDebugStatementWriter WriteLine(string text)
		{
			if (this.StartOfLine) Indent();
			this.Buffer.AppendLine(text);
			this.StartOfLine = true;
			return this;
		}

		public FdbDebugStatementWriter WriteLine()
		{
			return WriteLine(String.Empty);
		}

		public FdbDebugStatementWriter Write(string text)
		{
			if (this.StartOfLine) Indent();
			this.Buffer.Append(text);

			return this;
		}

		public FdbDebugStatementWriter WriteLine(string format, params string[] args)
		{
			return WriteLine(String.Format(CultureInfo.InvariantCulture, format, args));
		}

		public FdbDebugStatementWriter Write(string format, params string[] args)
		{
			return Write(String.Format(CultureInfo.InvariantCulture, format, args));
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
