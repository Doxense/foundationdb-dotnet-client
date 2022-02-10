﻿#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;

	/// <summary>Expression that uses an async sequence as the source of elements</summary>
	public sealed class FdbQueryAsyncEnumerableExpression<T> : FdbQuerySequenceExpression<T>
	{

		internal FdbQueryAsyncEnumerableExpression(IAsyncEnumerable<T> source)
		{
			Contract.Debug.Requires(source != null);
			this.Source = source;
		}

		/// <summary>Returns <see cref="FdbQueryShape.Sequence"/></summary>
		public override FdbQueryShape Shape => FdbQueryShape.Sequence;

		/// <summary>Source sequence of this expression</summary>
		public IAsyncEnumerable<T> Source { get; }

		/// <summary>Apply a custom visitor to this expression</summary>
		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitAsyncEnumerable(this);
		}

		/// <summary>Write a human-readable explanation of this expression</summary>
		public override void WriteTo(FdbQueryExpressionStringBuilder builder)
		{
			builder.Writer.Write("Source<{0}>({1})", this.ElementType.Name, this.Source.GetType().Name);
		}

		/// <summary>Returns a new expression that will execute this query on a transaction and return a single result</summary>
		public override Expression<Func<IFdbReadOnlyTransaction, CancellationToken, Task<IAsyncEnumerable<T>>>> CompileSingle()
		{
			return FdbExpressionHelpers.ToTask(CompileSequence());
		}

		/// <summary>Returns a new expression that creates an async sequence that will execute this query on a transaction</summary>
		public override Expression<Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>>> CompileSequence()
		{
			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>>>(
				Expression.Constant(this.Source), 
				prmTrans
			);
		}

	}

}
