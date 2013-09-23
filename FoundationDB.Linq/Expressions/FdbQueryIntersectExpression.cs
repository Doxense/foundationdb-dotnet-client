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
	using FoundationDB.Linq.Utils;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;

	/// <summary>Intersection between two or more sequence</summary>
	/// <typeparam name="T">Type of the keys returned</typeparam>
	public abstract class FdbQueryMergeExpression<T> : FdbQuerySequenceExpression<T>
	{

		internal FdbQueryMergeExpression(FdbQuerySequenceExpression<T>[] expressions, IComparer<T> keyComparer)
		{
			this.Expressions = expressions;
			this.KeyComparer = keyComparer;
		}

		public override FdbQueryShape Shape
		{
			get { return FdbQueryShape.Sequence; }
		}

		internal FdbQuerySequenceExpression<T>[] Expressions { get; private set; }

		public IReadOnlyList<FdbQuerySequenceExpression<T>> Terms { get { return this.Expressions; } }

		public IComparer<T> KeyComparer { get; private set; }

		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQueryMerge(this);
		}

		public override Expression<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>>> CompileSequence()
		{
			// compile the key selector
			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			// get all inner enumerables
			var enumerables = new Expression[this.Expressions.Length];
			for(int i=0;i<this.Expressions.Length;i++)
			{
				enumerables[i] = FdbExpressionHelpers.RewriteCall(this.Expressions[i].CompileSequence(), prmTrans);
			}

			var array = Expression.NewArrayInit(typeof(IFdbAsyncEnumerable<T>), enumerables);
			Expression body;
			switch (this.QueryNodeType)
			{
				case FdbQueryNodeType.Intersect:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<IFdbAsyncEnumerable<T>[], IComparer<T>, IFdbAsyncEnumerable<T>>>(
						(sources, comparer) => FdbMergeQueryExtensions.Intersect(sources, comparer),
						array,
						Expression.Constant(this.KeyComparer, typeof(IComparer<T>))
					);
					break;
				}
				case FdbQueryNodeType.Union:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<IFdbAsyncEnumerable<T>[], IComparer<T>, IFdbAsyncEnumerable<T>>>(
						(sources, comparer) => FdbMergeQueryExtensions.Union(sources, comparer),
						array,
						Expression.Constant(this.KeyComparer, typeof(IComparer<T>))
					);
					break;
				}
				default:
				{
					throw new InvalidOperationException(String.Format("Unsupported index merge mode {0}", this.QueryNodeType));
				}
			}

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>>>(
				body,
				prmTrans
			);
		
		}

	}

	public sealed class FdbQueryIntersectExpression<T> : FdbQueryMergeExpression<T>
	{

		internal FdbQueryIntersectExpression(FdbQuerySequenceExpression<T>[] expressions, IComparer<T> keyComparer)
			: base(expressions, keyComparer)
		{ }

		public override FdbQueryNodeType QueryNodeType
		{
			get { return FdbQueryNodeType.Intersect; }
		}

	}

	/// <summary>Intersection between two or more sequence</summary>
	/// <typeparam name="T">Type of the keys returned</typeparam>
	public sealed class FdbQueryUnionExpression<T> : FdbQueryMergeExpression<T>
	{

		internal FdbQueryUnionExpression(FdbQuerySequenceExpression<T>[] expressions, IComparer<T> keyComparer)
			: base(expressions, keyComparer)
		{ }

		public override FdbQueryNodeType QueryNodeType
		{
			get { return FdbQueryNodeType.Union; }
		}

	}

}
