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
	using FoundationDB.Client;
	using FoundationDB.Layers.Indexing;
	using System;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Threading;

	/// <summary>Expression that represents a lookup on an FdbIndex</summary>
	/// <typeparam name="K">Type of the Id of the enties being indexed</typeparam>
	/// <typeparam name="V">Type of the value that will be looked up</typeparam>
	public abstract class FdbQueryLookupExpression<K, V> : FdbQuerySequenceExpression<K>
	{

		protected FdbQueryLookupExpression(Expression source, ExpressionType op, Expression value)
		{
			this.Source = source;
			this.Operator = op;
			this.Value = value;
		}

		public Expression Source { get; private set; }

		public ExpressionType Operator { get; private set; }

		public Expression Value { get; private set; }

		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQueryLookup(this);
		}

		public override void WriteTo(FdbQueryExpressionStringBuilder builder)
		{
			builder.Visit(this.Source);
			builder.Writer.Write(".Lookup<{0}>(value {1} ", this.ElementType.Name, FdbExpressionHelpers.GetOperatorAlias(this.Operator));
			builder.Visit(this.Value);
			builder.Writer.Write(")");
		}

		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "{0}.Lookup({1}, {2})", this.Source.ToString(), this.Operator, this.Value);
		}

	}


	/// <summary>Expression that represents a lookup on an FdbIndex</summary>
	/// <typeparam name="K">Type of the Id of the enties being indexed</typeparam>
	/// <typeparam name="V">Type of the value that will be looked up</typeparam>
	public class FdbQueryIndexLookupExpression<K, V> : FdbQueryLookupExpression<K, V>
	{

		internal FdbQueryIndexLookupExpression(FdbIndex<K, V> index, ExpressionType op, Expression value)
			: base(Expression.Constant(index), op, value)
		{
			this.Index = index;
		}

		public FdbIndex<K, V> Index { get; private set; }

		public override Expression<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<K>>> CompileSequence()
		{
			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");
			Expression body;

			switch (this.Operator)
			{
				case ExpressionType.Equal:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<FdbIndex<K, V>, IFdbReadOnlyTransaction, V, bool, IFdbAsyncEnumerable<K>>>(
						(index, trans, value, reverse) => index.Lookup(trans, value, reverse),
						Expression.Constant(this.Index, typeof(FdbIndex<K, V>)),
						prmTrans,
						this.Value,
						Expression.Constant(false, typeof(bool)) // reverse
					);
					break;
				}

				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<FdbIndex<K, V>, IFdbReadOnlyTransaction, V, bool, IFdbAsyncEnumerable<K>>>(
						(index, trans, value, reverse) => index.LookupGreaterThan(trans, value, this.Operator == ExpressionType.GreaterThanOrEqual, reverse),
						Expression.Constant(this.Index, typeof(FdbIndex<K, V>)),
						prmTrans,
						this.Value,
						Expression.Constant(false, typeof(bool)) // reverse
					);
					break;
				}

				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<FdbIndex<K, V>, IFdbReadOnlyTransaction, V, bool, IFdbAsyncEnumerable<K>>>(
						(index, trans, value, reverse) => index.LookupLessThan(trans, value, this.Operator == ExpressionType.LessThanOrEqual, reverse),
						Expression.Constant(this.Index, typeof(FdbIndex<K, V>)),
						prmTrans,
						this.Value,
						Expression.Constant(false, typeof(bool)) // reverse
					);
					break;
				}

				default:
				{
					throw new NotImplementedException();
				}
			}

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<K>>>(body, prmTrans);
		}

		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "Index['{0}'].Lookup({1}, {2})", this.Index.Name, this.Operator, this.Value);
		}

		public static FdbQueryIndexLookupExpression<K, V> Lookup(FdbIndex<K, V> index, ExpressionType op, Expression value)
		{
			if (index == null) throw new ArgumentNullException("index");
			if (value == null) throw new ArgumentNullException("value");

			switch (op)
			{
				case ExpressionType.Equal:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
					break;
				default:
					throw new ArgumentException("Index lookups only support the following operators: '==', '!=', '>', '>=', '<' and '<='", "op");
			}

			//TODO: IsAssignableFrom?
			if (value.Type != typeof(V)) throw new ArgumentException("Value must have a type compatible with the index", "value");

			return new FdbQueryIndexLookupExpression<K, V>(index, op, value);
		}

		public static FdbQueryIndexLookupExpression<K, V> Lookup(FdbIndex<K, V> index, Expression<Func<V, bool>> expression)
		{
			if (index == null) throw new ArgumentNullException("index");

			var binary = expression.Body as BinaryExpression;
			if (binary == null) throw new ArgumentException("Only binary expressions are allowed", "expression");

			var constant = binary.Right as ConstantExpression;
			if (constant == null) throw new ArgumentException(String.Format("Left side of expression '{0}' must be a constant of type {1}", binary.Right.ToString(), typeof(V).Name));

			return Lookup(index, binary.NodeType, constant);
		}
	}

}
