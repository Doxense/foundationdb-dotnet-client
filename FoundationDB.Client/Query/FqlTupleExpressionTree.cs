#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{

	/// <summary>Tree that can match a key against a collection of expressions</summary>
	public sealed class FqlTupleExpressionTree
	{

		/// <summary>Root node of the tree</summary>
		private Node Root { get; }

		/// <summary>List of the matched expressions</summary>
		private FqlTupleExpression[] Expressions { get; }

		internal FqlTupleExpressionTree(Node root, FqlTupleExpression[] expressions)
		{
			this.Root = root;
			this.Expressions = expressions;
		}

		/// <summary>Finds the expression that matches the given tuple</summary>
		/// <param name="tuple">Tuple to match against the tree</param>
		/// <returns>Matching expression, or <c>null</c> if no match was found</returns>
		public FqlTupleExpression? Match(SlicedTuple tuple)
		{
			var node = this.Root;

			int n = tuple.Count;
			for (int i = 0; i < n; i++)
			{

				if (node.CatchAll is not null)
				{
					return this.Expressions[node.CatchAll.Value];
				}

				var next = node.Evaluate(tuple);
				if (next == null)
				{
					return null;
				}
				node = next;
			}

			if (node.Match is not null)
			{
				return this.Expressions[node.Match.Value];
			}

			if (node.CatchAll is not null)
			{
				return this.Expressions[node.CatchAll.Value];
			}

			return null;
		}

		/// <summary>Internal node in a <see cref="FqlTupleExpressionTree"/></summary>
		[DebuggerDisplay("Depth={Depth}, Label={Label}")]
		internal sealed class Node
		{

			public required int Depth { get; init; }

			public string? Label { get; init; }

			/// <summary>If non-null, list of conditions that lead to the next nodes</summary>
			public Dictionary<FqlTupleItem, Node>? Children { get; set; }

			/// <summary>If not-null, matching expression if evaluation ends up in this node (not on a MaybeMore)</summary>
			public int? Match { get; set; }

			/// <summary>If not-null, matching expression if evaluation ends up in this node, and the last item is a MaybeMore</summary>
			public int? CatchAll { get; set; }

			/// <summary>Evaluate the next node that matches the tuple at the current depth</summary>
			public Node? Evaluate(SlicedTuple tuple)
			{
				if (this.Children is not null)
				{
					var candidateType = tuple.GetElementType(this.Depth);
					foreach (var (item, next) in this.Children)
					{
						switch (item.Type)
						{
							case FqlItemType.Integer:
							{
								if (candidateType == TupleSegmentType.Integer
								    && item.Matches(tuple.GetSpan(this.Depth))) //OPTIMIZE: PERF: TODO: !!!
								{
									return next;
								}
								break;
							}
							case FqlItemType.String:
							{
								if (candidateType == TupleSegmentType.UnicodeString
								    && item.Matches(tuple.GetSpan(this.Depth))) //OPTIMIZE: PERF: TODO: !!!
								{
									return next;
								}
								break;
							}
							case FqlItemType.Tuple:
							{
								if (candidateType == TupleSegmentType.Tuple
								    && item.Matches(tuple.GetSpan(this.Depth))) //OPTIMIZE: PERF: TODO: !!!
								{
									return next;
								}
								break;
							}
							case FqlItemType.Bytes:
							{
								if (candidateType == TupleSegmentType.ByteString
								    && item.Matches(tuple.GetSpan(this.Depth))) //OPTIMIZE: PERF: TODO: !!!
								{
									return next;
								}
								break;
							}
							case FqlItemType.VStamp:
							{
								if (candidateType is (TupleSegmentType.VersionStamp80 or TupleSegmentType.VersionStamp96)
								    && item.Matches(tuple.GetSpan(this.Depth))) //OPTIMIZE: PERF: TODO: !!!
								{
									return next;
								}
								break;
							}

							case FqlItemType.Variable:
							{
								var types = (FqlVariableTypes) item.Value!;

								switch (candidateType)
								{
									case TupleSegmentType.ByteString:
									{
										if (types.HasFlag(FqlVariableTypes.Bytes)) return next;
										break;
									}
									case TupleSegmentType.UnicodeString:
									{
										if (types.HasFlag(FqlVariableTypes.String)) return next;
										break;
									}
									case TupleSegmentType.Integer:
									{
										if (types.HasFlag(FqlVariableTypes.Int)) return next;
										break;
									}
									case TupleSegmentType.Boolean:
									{
										if (types.HasFlag(FqlVariableTypes.Bool)) return next;
										break;
									}
									case TupleSegmentType.VersionStamp80 or TupleSegmentType.VersionStamp96:
									{
										if (types.HasFlag(FqlVariableTypes.VStamp)) return next;
										break;
									}
								}
								break;
							}
						}
					}
				}

				return null;

			}

		}

		public static Builder CreateBuilder() => new Builder();

		/// <summary>Builder for generating a tree of multiple <see cref="FqlTupleExpression"/></summary>
		public sealed class Builder
		{

			public List<FqlTupleExpression> Expressions { get; } = [ ];

			public void AddExpression(FqlTupleExpression expr)
			{
				this.Expressions.Add(expr);
			}

			public FqlTupleExpressionTree BuildDfaTree()
			{

				var root = new Node()
				{
					Depth = 0,
					Label = "/",
				};

				var maxDepth = 0;
				for (int exprIdx = 0; exprIdx < this.Expressions.Count; exprIdx++)
				{
					var expr = this.Expressions[exprIdx];

					var node = root!;

					bool lastIsMaybeMore = false;
					for (int i = 0; i < expr.Items.Count; i++)
					{
						var item = expr.Items[i];

						if (item.Type == FqlItemType.MaybeMore)
						{
							lastIsMaybeMore = true;
							break;
						}

						var children = node.Children ??= [ ];

						ref var next = ref CollectionsMarshal.GetValueRefOrAddDefault(children, item, out var exists);
						if (!exists)
						{
							next = new()
							{
								Depth = node.Depth + 1,
								Label = item.ToString(),
							};
						}
						node = next!;
					}

					if (lastIsMaybeMore)
					{
						node.CatchAll = exprIdx;
					}
					else
					{
						node.Match = exprIdx;
					}

					maxDepth = Math.Max(maxDepth, expr.Items.Count);
				}

				return new(root, [ ..this.Expressions ]);
			}

		}

	}

}

#endif
