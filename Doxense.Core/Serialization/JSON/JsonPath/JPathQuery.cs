#region Copyright Doxense 2010-2017
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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
			return ParseExpression(queryText).Iterate(root, root);
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
			var context = new JPathParser { Path = queryText };
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
