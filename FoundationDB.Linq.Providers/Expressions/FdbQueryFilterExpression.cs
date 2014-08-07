#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Linq.Expressions;
	using System.Threading;

	/// <summary>Expression that represent a filter on a source sequence</summary>
	/// <typeparam name="T">Type of elements in the source sequence</typeparam>
	public class FdbQueryFilterExpression<T> : FdbQuerySequenceExpression<T>
	{

		internal FdbQueryFilterExpression(FdbQuerySequenceExpression<T> source, Expression<Func<T, bool>> filter)
		{
			Contract.Requires(source != null && filter != null);
			this.Source = source;
			this.Filter = filter;
		}

		/// <summary>Source sequence that is being filtered</summary>
		public FdbQuerySequenceExpression<T> Source
		{
			[NotNull] get;
			private set;
		}

		/// <summary>Filter applied on each element of <see cref="Source"/> and that must return true for the element to be outputed</summary>
		public Expression<Func<T, bool>> Filter
		{
			[NotNull] get;
			private set;
		}

		/// <summary>Apply a custom visitor to this expression</summary>
		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQueryFilter(this);
		}

		/// <summary>Write a human-readable explanation of this expression</summary>
		public override void WriteTo([NotNull] FdbQueryExpressionStringBuilder builder)
		{
			builder.Writer.WriteLine("Filter(").Enter();
			builder.Visit(this.Source);
			builder.Writer.WriteLine(",");
			builder.Visit(this.Filter);
			builder.Writer.Leave().Write(")");
		}

		/// <summary>Returns a new expression that creates an async sequence that will execute this query on a transaction</summary>
		[NotNull]
		public override Expression<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>>> CompileSequence()
		{
			var lambda = this.Filter.Compile();

			var enumerable = this.Source.CompileSequence();

			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			// (tr) => sourceEnumerable(tr).Where(lambda);

			var body = FdbExpressionHelpers.RewriteCall<Func<IFdbAsyncEnumerable<T>, Func<T, bool>, IFdbAsyncEnumerable<T>>>(
				(sequence, predicate) => sequence.Where(predicate),
				FdbExpressionHelpers.RewriteCall(enumerable, prmTrans),
				Expression.Constant(lambda)
			);

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>>>(body, prmTrans);
		}

	}

}
