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

	/// <summary>Represents "template" for a key/value that can be decoded into a better text representation</summary>
	public sealed class FqlTemplateExpression
	{

		/// <summary>Name of the template (ideally unique per "schema")</summary>
		public string Name { get; }

		/// <summary>Expression for the tuples that match this template</summary>
		public FqlTupleExpression Expression { get; }

		/// <summary>Hinter for the values of keys that match this template</summary>
		public FqlValueTypeHinter Hinter { get; }

		public FqlTemplateExpression(string name, FqlTupleExpression expr, FqlValueTypeHinter hinter)
		{
			this.Name = name;
			this.Expression = expr;
			this.Hinter = hinter;
		}

		public FqlTemplateExpression(string name, FqlTupleExpression expr, FqlValueTypeHint hint)
		{
			this.Name = name;
			this.Expression = expr;
			this.Hinter = (_) => hint;
		}

	}

	/// <summary>Tree that can match a key against a collection of <see cref="FqlTemplateExpression"/>></summary>
	public sealed class FqlTemplateTree
	{

		/// <summary>Root node of the tree</summary>
		private Node Root { get; }

		/// <summary>List of the templates matched by this tree</summary>
		private FqlTemplateExpression[] Templates { get; }

		internal FqlTemplateTree(Node root, FqlTemplateExpression[] templates)
		{
			this.Root = root;
			this.Templates = templates;
		}

		/// <summary>Finds the expression that matches the given tuple</summary>
		/// <param name="tuple">Tuple to match against the tree</param>
		/// <returns>Matching expression, or <c>null</c> if no match was found</returns>
		public bool TryMatch(SpanTuple tuple, [MaybeNullWhen(false)] out FqlTemplateExpression template)
		{
			var node = this.Root;

			int n = tuple.Count;

			// holds a "catchall" clause that would have been encountered at one point
			int? candidate = null;

			for (int i = 0; i < n; i++)
			{

				if (node.CatchAll is not null)
				{
					candidate = node.CatchAll;
				}

				var next = node.Evaluate(tuple);
				if (next == null)
				{
					goto no_direct_match;
				}
				node = next;
			}

			if (node.Match is not null)
			{ // we have a direct match
				template = this.Templates[node.Match.Value];
				return true;
			}

		no_direct_match:

			if (node.CatchAll is not null)
			{
				candidate = node.CatchAll;
			}
			if (candidate is not null)
			{
				template = this.Templates[candidate.Value];
				return true;
			}

			template = null;
			return false;
		}

		/// <summary>Internal node in a <see cref="FqlTemplateTree"/></summary>
		[DebuggerDisplay("Depth={Depth}, Label={Label}")]
		internal sealed record Node
		{

			/// <summary>Index of the matched item in the tuple</summary>
			public required int Depth { get; init; }

			/// <summary>Label (for debugging purpose)</summary>
			public string? Label { get; init; }

			/// <summary>If non-null, list of conditions that lead to the next nodes</summary>
			public Dictionary<FqlTupleItem, Node>? Children { get; set; }

			/// <summary>If not-null, matching expression if evaluation ends up in this node (not on a MaybeMore)</summary>
			public int? Match { get; set; }

			/// <summary>If not-null, matching expression if evaluation ends up in this node, and the last item is a MaybeMore</summary>
			public int? CatchAll { get; set; }

			/// <summary>Evaluate the next node that matches the tuple at the current depth</summary>
			public Node? Evaluate(SpanTuple tuple)
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
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.String:
							{
								if (candidateType == TupleSegmentType.UnicodeString
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.Tuple:
							{
								if (candidateType == TupleSegmentType.Tuple
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.Bytes:
							{
								if (candidateType == TupleSegmentType.ByteString
								    && item.Matches(tuple.GetSpan(this.Depth)))
								{
									return next;
								}
								break;
							}
							case FqlItemType.VStamp:
							{
								if (candidateType is (TupleSegmentType.VersionStamp80 or TupleSegmentType.VersionStamp96)
								    && item.Matches(tuple.GetSpan(this.Depth)))
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

		/// <summary>Creates a new <see cref="Builder"/> that can compile a set of <see cref="FqlTemplateExpression"/> into a <see cref="FqlTemplateTree"/></summary>
		public static Builder CreateBuilder() => new();

		/// <summary>Builder for generating a tree of multiple <see cref="FqlTemplateExpression"/></summary>
		public sealed class Builder
		{

			/// <summary>Templates added to this builder</summary>
			public List<FqlTemplateExpression> Templates { get; } = [ ];

			public void Add(string name, FqlTupleExpression expr, FqlValueTypeHinter? hinter = null)
			{
				Contract.NotNullOrEmpty(name);
				Contract.NotNull(expr);

				hinter ??= (_) => FqlValueTypeHint.None;
				var template = new FqlTemplateExpression(name, expr, hinter);
				Add(template);
			}

			public void Add(FqlTemplateExpression template)
			{
				Contract.NotNull(template);
				this.Templates.Add(template);
			}

			/// <summary>Compiles the templates into a <see cref="FqlTemplateTree"/></summary>
			public FqlTemplateTree BuildTree()
			{
				var root = new Node()
				{
					Depth = 0,
					Label = "/",
				};

				for (int exprIdx = 0; exprIdx < this.Templates.Count; exprIdx++)
				{
					var template = this.Templates[exprIdx];

					var node = root!;

					bool lastIsMaybeMore = false;
					foreach (var item in template.Expression.Items)
					{
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
				}

				return new(root, [ ..this.Templates ]);
			}

		}

		/// <summary>Enumerates the templates used by this tree</summary>
		public IEnumerable<FqlTemplateExpression> GetTemplates()
		{
			return this.Templates;
		}

	}

	/// <summary>Represents the possible type for a value in the database</summary>
	public enum FqlValueTypeHint
	{
		/// <summary>The type is unknown.</summary>
		None = 1,

		/// <summary>The value is raw binary that should be displayed as a raw hexadecimal dump</summary>
		/// <remarks>This type can be used for files, binary blobs, or encoded/encrypted data.</remarks>
		Binary,

		/// <summary>The value is encoded using the Tuple format</summary>
		/// <remarks>See <see cref="TuPack.Unpack(System.Slice)"/></remarks>
		Tuple,

		/// <summary>The value is a 7-bit ASCII encoded string.</summary>
		Ascii,

		/// <summary>The value is a UTF-8 encoded string.</summary>
		/// <remarks>The value may include a UTF-8 BOM.</remarks>
		Utf8,

		/// <summary>The value is a little-endian integer value, that is best displayed in base-10.</summary>
		IntegerLittleEndian,

		/// <summary>The value is a big-endian integer value, that is best displayed in base-10.</summary>
		IntegerBigEndian,

		/// <summary>The value is a little-endian integer value, that is best displayed in base-16.</summary>
		IntegerHexLittleEndian,

		/// <summary>The value is a big-endian integer value, that is best displayed in base-16.</summary>
		IntegerHexBigEndian,

		/// <summary>The value is an integer that represents the number of elapsed milliseconds since the Unix epoch.</summary>
		UnixTimeMillis,

		/// <summary>The value is a 32-bit IEEE floating point number.</summary>
		Single,

		/// <summary>The value is a 64-bit IEEE floating point number.</summary>
		Double,

		/// <summary>The value is a 128-bit <see cref="Uuid128"/></summary>
		Uuid128,

		/// <summary>The value is a 96-bit <see cref="Uuid96"/></summary>
		Uuid96,
		
		/// <summary>The value is an 80-bit <see cref="Uuid80"/></summary>
		Uuid80,
		
		/// <summary>The value is a 64-bit <see cref="Uuid64"/></summary>
		Uuid64,
		
		/// <summary>The value is a 48-bit <see cref="Uuid48"/></summary>
		Uuid48,

		/// <summary>The value is <see cref="VersionStamp"/></summary>
		VersionStamp,

		/// <summary>The value is a JSON Document (UTF-8)</summary>
		Json,

	}

	public delegate FqlValueTypeHint FqlValueTypeHinter(SpanTuple tuple);

}

#endif
