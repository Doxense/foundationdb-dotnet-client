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
	using FoundationDB.Layers.Indexing;
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;

	public class FdbQueryExpressionStringBuilder : FdbQueryExpressionVisitor
	{
		private readonly FdbDebugStatementWriter m_writer;

		public FdbQueryExpressionStringBuilder()
			: this(null)
		{ }

		public FdbQueryExpressionStringBuilder(FdbDebugStatementWriter writer)
		{
			m_writer = writer ?? new FdbDebugStatementWriter();
		}

		public override string ToString()
		{
			return m_writer.Buffer.ToString();
		}

		protected internal override Expression VisitQuerySingle<T, R>(FdbQuerySingleExpression<T, R> node)
		{
			m_writer.WriteLine("{0}(", node.Name).Enter();
			Visit(node.Sequence);
			m_writer.Leave().Write(")");

			return node;
		}

		protected internal override Expression VisitQueryIndexLookup<K, V>(FdbQueryIndexLookupExpression<K, V> node)
		{
			VisitIndex(node.Index);
			m_writer.Write(".Lookup<{0}>(value {1} ", node.ElementType.Name, FdbExpressionHelpers.GetOperatorAlias(node.Operator));
			Visit(node.Value);
			m_writer.Write(")");

			return node;
		}

		protected internal virtual void VisitIndex<K, V>(FdbIndex<K, V> index)
		{
			m_writer.Write("Index['{0}']", index.Name);
		}

		protected internal override Expression VisitQueryRange(FdbQueryRangeExpression node)
		{
			m_writer.WriteLine("Range(").Enter()
				.WriteLine("Start({0}),", node.Range.Begin.ToString())
				.WriteLine("Stop({0})", node.Range.End.ToString())
			.Leave().Write(")");

			return node;
		}

		protected internal override Expression VisitQueryMerge<T>(FdbQueryMergeExpression<T> node)
		{
			m_writer.WriteLine("{0}<{1}>(", node.MergeType.ToString(), node.ElementType.Name).Enter();
			for (int i = 0; i < node.Expressions.Length; i++)
			{
				Visit(node.Expressions[i]);
				if (i + 1 < node.Expressions.Length)
					m_writer.WriteLine(",");
				else
					m_writer.WriteLine();
			}
			m_writer.Leave().Write(")");

			return node;
		}

		protected internal override Expression VisitAsyncEnumerable<T>(FdbQueryAsyncEnumerableExpression<T> node)
		{
			m_writer.Write("Source<{0}>({1})", node.ElementType.Name, node.Source.GetType().Name);

			return node;
		}

		protected internal override Expression VisitQueryTransform<T, R>(FdbQueryTransformExpression<T, R> node)
		{
			m_writer.WriteLine("Transform(").Enter();
			Visit(node.Source);
			m_writer.WriteLine(",");
			Visit(node.Transform);
			m_writer.Leave().Write(")");

			return node;
		}

		protected internal override Expression VisitQueryFilter<T>(FdbQueryFilterExpression<T> node)
		{
			m_writer.WriteLine("Filter(").Enter();
			Visit(node.Source);
			m_writer.WriteLine(",");
			Visit(node.Filter);
			m_writer.Leave().Write(")");

			return node;
		}

		private void VisitExpressions<TExpr>(IList<TExpr> expressions, string open, string close, string sep)
			where TExpr : Expression
		{
			if (expressions == null || expressions.Count == 0)
			{
				m_writer.Write(open).Write(close);
				return;
			}

			m_writer.Write(open);
			bool first = true;
			foreach(var expr in expressions)
			{
				if (first) first = false; else m_writer.Write(sep);
				Visit(expr);
			}
			m_writer.Write(close);
		}

		private void VisitValues<T>(IList<T> values, Action<T> handler, string open, string close, string sep)
		{
			if (values == null || values.Count == 0)
			{
				m_writer.Write(open).Write(close);
				return;
			}

			m_writer.Write(open);
			bool first = true;
			foreach (var expr in values)
			{
				if (first) first = false; else m_writer.Write(sep);
				handler(expr);
			}
			m_writer.Write(close);
		}

		protected override Expression VisitLambda<T>(Expression<T> node)
		{
			VisitExpressions(node.Parameters, "(", ")", ", ");
			m_writer.Write(" => ");
			Visit(node.Body);

			return node;
		}

		protected override Expression VisitParameter(ParameterExpression node)
		{
			m_writer.Write(node.Name ?? "<param>");

			return node;
		}

		protected override Expression VisitMember(MemberExpression node)
		{
			Visit(node.Expression);
			m_writer.Write(".").Write(node.Member.Name);

			return node;
		}

		protected override Expression VisitBinary(BinaryExpression node)
		{
			Visit(node.Left);
			m_writer.Write(" {0} ", FdbExpressionHelpers.GetOperatorAlias(node.NodeType));
			Visit(node.Right);

			return node;
		}

		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			if (node.Object == null)
			{
				m_writer.Write(node.Method.DeclaringType.Name);
			}
			else
			{
				Visit(node.Object);
			}

			m_writer.Write(".{0}", node.Method.Name);

			if (node.Method.IsGenericMethod)
			{
				VisitValues(node.Method.GetGenericArguments(), (type) => m_writer.Write(type.Name), "<", ">", ", ");
			}

			if (node.Arguments != null)
			{
				VisitExpressions(node.Arguments, "(", ")", ", ");
			}

			return node;
		}

		protected override Expression VisitConstant(ConstantExpression node)
		{
			m_writer.Write(node.GetDebugView());
			return node;
		}

		public static void Test()
		{

			Expression<Func<int, int, string>> func = (x, y) => String.Format("{0} x {1} == {2}", x, y, (x * y));

			var w = new FdbQueryExpressionStringBuilder();
			w.Visit(func);

			Console.WriteLine(w.ToString());

		}

	}

}
