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

namespace FoundationDB.Client
{
	using System.IO;

	//TODO: maybe move this to a more global namespace?

	/// <summary>Interface for expressions or AST nodes that can generate a human-readable description of their part in a query or command</summary>
	public interface ICanExplain
	{

		/// <summary>Adds a human-readable description of this node into the log</summary>
		/// <param name="output">Output where the description should be written</param>
		/// <param name="depth">Current depth level (add a TAB (<c>'\t'</c>) for each depth level</param>
		/// <param name="recursive">If <see langword="true"/> (default), also explain any children of this node; otherwise, only give a brief one-line summary.</param>
		void Explain(TextWriter output, int depth = 0, bool recursive = true);

	}

	/// <summary>Extensions methods for <see cref="ICanExplain"/> types</summary>
	public static class CanExplainExtensions
	{

		/// <summary>Returns a human-readable description of what this node does</summary>
		/// <param name="node">Node that should explain itself</param>
		/// <param name="recursive">If <see langword="true"/> (default), also explain any children of this node; otherwise, only give a brief one-line summary.</param>
		/// <returns>Description of this node</returns>
		/// <remarks>The text will usually end with an extra newline (<c>\r\n</c>)</remarks>
		public static string Explain(this ICanExplain node, bool recursive = true)
		{
			var sb = new StringWriter();
			node.Explain(sb, 0, recursive);
			return sb.ToString();
		}

	}

}
