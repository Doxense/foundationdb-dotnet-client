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

namespace Doxense.Serialization.Json
{
	using Doxense.Linq;

	/// <summary>Node in the hierarchy of a document proxy</summary>
	public interface IJsonProxyNode
	{

		/// <summary>Parent of this node</summary>
		/// <remarks>If <c>null</c>, this node if the root of the wrapped document</remarks>
		IJsonProxyNode? Parent { get; }

		/// <summary>Segment of path from the parent to this node</summary>
		JsonPathSegment Segment { get; }

		/// <summary>Type of JSON value wrapped by this node</summary>
		JsonType Type { get; }

		/// <summary>Number of nodes between the root and this node</summary>
		int Depth { get; }

		/// <summary>Write the path from the root to this node</summary>
		/// <param name="builder">Builder for the path</param>
		void WritePath(ref JsonPathBuilder builder);

	}

	public static class JsonProxyNodeExtensions
	{

		public static JsonPath GetPath(this IJsonProxyNode self)
		{
			// in most cases, we don't have any parent
			var parent = self.Parent;
			if (parent == null)
			{
				return JsonPath.Create(self.Segment);
			}

			return GetPathMultiple(self, parent);

			static JsonPath GetPathMultiple(IJsonProxyNode node, IJsonProxyNode? parent)
			{
				using var buf = new ValueBuffer<IJsonProxyNode>(8);
				while (parent != null)
				{
					buf.Add(node);
					node = parent;
					parent = node.Parent;
				}

				Span<char> scratch = stackalloc char[32];
				using var writer = new JsonPathBuilder(scratch);

				var stack = buf.Span;
				for (int i = stack.Length - 1; i >= 0; i--)
				{
					writer.Append(stack[i].Segment);
				}
				return writer.ToPath();
			}
		}

	}

}
