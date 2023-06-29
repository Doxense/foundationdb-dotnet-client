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
