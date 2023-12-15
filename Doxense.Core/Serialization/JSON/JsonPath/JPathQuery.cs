#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Serialization.Json.JsonPath
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	[DebuggerDisplay("{{Text},nq}")]
	public sealed class JPathQuery
	{

		public JPathExpression Expression { get; }

		public string Text { get; }

		internal JPathQuery(JPathExpression expression, string text)
		{
			Contract.NotNull(expression);
			this.Expression = expression;
			this.Text = text;
		}

		public override string ToString()
		{
			return this.Text;
		}

		[Pure]
		public JsonValue FirstOrDefault(JsonValue root)
		{
			if (root.IsNullOrMissing()) return JsonNull.Missing;
			return FirstOrDefault(root, this.Expression);
		}

		[Pure]
		public IEnumerable<JsonValue> Select(JsonValue root)
		{
			if (root.IsNullOrMissing()) return Array.Empty<JsonValue>();
			return Select(root, this.Expression);
		}

		[Pure]
		public static JsonValue FirstOrDefault(JsonValue root, string queryText)
		{
			Contract.NotNull(queryText);
			if (root.IsNullOrMissing()) return JsonNull.Missing;
			return ParseExpression(queryText).Iterate(root, root).FirstOrDefault() ?? JsonNull.Missing;
		}

		[Pure]
		public static JsonValue FirstOrDefault(JsonValue root, JPathExpression expression)
		{
			Contract.NotNull(expression);
			if (root.IsNullOrMissing()) return JsonNull.Missing;
			return expression.Iterate(root, root).FirstOrDefault() ?? JsonNull.Missing;
		}

		[Pure]
		public static IEnumerable<JsonValue> Select(JsonValue root, string queryText)
		{
			Contract.NotNull(queryText);
			if (root.IsNullOrMissing()) return Array.Empty<JsonValue>();
			var expr = ParseExpression(queryText);
			return expr.Iterate(root, root);
		}

		[Pure]
		public static IEnumerable<JsonValue> Select(JsonValue root, JPathExpression expression)
		{
			Contract.NotNull(expression);
			if (root.IsNullOrMissing()) return Array.Empty<JsonValue>();
			return expression.Iterate(root, root);
		}

		/// <summary>Parse the given JPath expression</summary>
		[Pure]
		public static JPathExpression ParseExpression(string queryText)
		{
			var context = new JPathParser(queryText.AsSpan());
			return context.ParseExpression(JPathExpression.Root);
		}

		[Pure]
		public static JPathQuery Parse(string queryText)
		{
			Contract.NotNullOrEmpty(queryText);
			return new JPathQuery(ParseExpression(queryText), queryText);
		}

	}

}
