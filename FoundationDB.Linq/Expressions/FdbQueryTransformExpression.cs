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
	using System;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;

	/// <summary>Expression that represent a projection from one type into another</summary>
	/// <typeparam name="T">Type of elements in the inner sequence</typeparam>
	/// <typeparam name="R">Type of elements in the outer sequence</typeparam>
	public class FdbQueryTransformExpression<T, R> : FdbQuerySequenceExpression<R>
	{

		internal FdbQueryTransformExpression(FdbQuerySequenceExpression<T> source, Expression<Func<T, R>> transform)
		{
			this.Source = source;
			this.Transform = transform;
		}

		public FdbQuerySequenceExpression<T> Source { get; private set; }

		public Expression<Func<T, R>> Transform { get; private set; }

		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQueryTransform(this);
		}

		public override void WriteTo(FdbQueryExpressionStringBuilder builder)
		{
			builder.Writer.WriteLine("Transform(").Enter();
			builder.Visit(this.Source);
			builder.Writer.WriteLine(",");
			builder.Visit(this.Transform);
			builder.Writer.Leave().Write(")");
		}

		public override Expression<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<R>>> CompileSequence()
		{
			var lambda = this.Transform.Compile();

			var enumerable = this.Source.CompileSequence();

			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			// (tr) => sourceEnumerable(tr).Select(lambda);

			var body = FdbExpressionHelpers.RewriteCall<Func<IFdbAsyncEnumerable<T>, Func<T, R>, IFdbAsyncEnumerable<R>>>(
				(sequence, selector) => sequence.Select(selector),
				FdbExpressionHelpers.RewriteCall(enumerable, prmTrans),
				Expression.Constant(lambda)
			);

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<R>>>(body, prmTrans);
		}

	}

}
