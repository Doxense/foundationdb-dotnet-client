#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Serialization.Json.JsonPath;

	public static class JPathExtensions
	{

		/// <summary>Returns the first node that matched the given JPath expression</summary>
		/// <param name="root">Root node of the query</param>
		/// <param name="query">JPath expression</param>
		/// <returns>First matching node, or <see cref="JsonNull.Missing"/> if none matches the query</returns>
		public static JsonValue Find(this JsonValue root, string query)
		{
			return JPathQuery.FirstOrDefault(root, query);
		}

		/// <summary>Returns the list of all nodes that match the given JPath expression</summary>
		/// <param name="root">Root node of the query</param>
		/// <param name="query">JPath expression</param>
		/// <returns>List of all matching nodes, or empty list if none matches the query</returns>
		public static List<JsonValue> FindAll(this JsonValue root, string query)
		{
			return JPathQuery.Select(root, query).ToList();
		}

	}
}
