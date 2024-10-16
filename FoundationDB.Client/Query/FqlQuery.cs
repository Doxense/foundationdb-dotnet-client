#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{
	using System.IO;

	public sealed class FqlQuery : IFqlQuery
	{

		public FqlDirectoryExpression? Directory { get; init; }

		public FqlTupleExpression? Tuple { get; init; }

		public override string ToString()
		{
			string? s = null;

			if (this.Directory != null)
			{
				s += this.Directory.ToString();
			}

			if (this.Tuple != null)
			{
				s += this.Tuple.ToString();
			}

			return s ?? "";
		}

		/// <inheritdoc />
		public bool IsPattern => (this.Directory?.IsPattern ?? false) || (this.Tuple?.IsPattern ?? false);

		/// <inheritdoc />
		public void Explain(TextWriter output, int depth = 0, bool recursive = true)
		{
			string indent = new string('\t', depth) + (depth == 0 ? "" : " -");

			if (!recursive)
			{
				output.WriteLine($"{indent}Query: `{ToString()}`");
				return;
			}

			output.WriteLine($"{indent}Query: `{ToString()}`");
			if (this.Directory != null)
			{
				this.Directory.Explain(output, depth + 1, true);
			}
			else
			{
				output.WriteLine($"{indent}\t- Directory: <none>");
			}

			if (this.Tuple != null)
			{
				this.Tuple.Explain(output, depth + 1, true);
			}
			else
			{
				output.WriteLine($"{indent}\t- Tuple: <none>");
			}
		}

	}

}
#endif
