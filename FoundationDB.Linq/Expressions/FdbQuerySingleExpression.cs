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
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Linq.Utils;
	using System;
	using System.Diagnostics.Contracts;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbQuerySingleExpression<T, R> : FdbQueryExpression<R>
	{
		public FdbQuerySingleExpression(FdbQuerySequenceExpression<T> sequence, string name, Expression<Func<IFdbAsyncEnumerable<T>, CancellationToken, Task<R>>> lambda)
		{
			this.Sequence = sequence;
			this.Name = name;
			this.Lambda = lambda;
		}

		public override FdbQueryNodeType QueryNodeType
		{
			get { return FdbQueryNodeType.Single; }
		}

		public override FdbQueryShape Shape
		{
			get { return FdbQueryShape.Single; }
		}

		public FdbQuerySequenceExpression<T> Sequence { get; private set;}

		public string Name { get; private set; }

		public new Expression<Func<IFdbAsyncEnumerable<T>, CancellationToken, Task<R>>> Lambda { get; private set; }

		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQuerySingle(this);
		}

		public override Expression<Func<IFdbReadTransaction, CancellationToken, Task<R>>> CompileSingle()
		{
			// We want to generate: (trans, ct) => ExecuteEnumerable(source, lambda, trans, ct);

			var sourceExpr = this.Sequence.CompileSequence();

			var prmTrans = Expression.Parameter(typeof(IFdbReadTransaction), "trans");
			var prmCancel = Expression.Parameter(typeof(CancellationToken), "ct");

			var method = typeof(FdbExpressionHelpers).GetMethod("ExecuteEnumerable", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(T), typeof(R));
			Contract.Assert(method != null, "ExecuteEnumerable helper method is missing!");

			var body = Expression.Call(
				method,
				sourceExpr,
				this.Lambda,
				prmTrans,
				prmCancel);

			return Expression.Lambda<Func<IFdbReadTransaction, CancellationToken, Task<R>>>(
				body,
				prmTrans,
				prmCancel
			);
		}

		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "{0}({1})", this.Name, this.Sequence.ToString());
		}
	}

}
