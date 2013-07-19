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

namespace FoundationDB.Linq
{
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq.Expressions;
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbAsyncQuery : IFdbDatabaseQueryable, IFdbAsyncQueryProvider
	{

		internal FdbAsyncQuery(FdbDatabase db)
		{
			this.Database = db;
		}

		internal FdbAsyncQuery(IFdbTransaction trans)
		{
			this.Transaction = trans;
		}

		internal FdbAsyncQuery(FdbDatabase db, FdbQueryExpression expression)
		{
			this.Database = db;
			this.Expression = expression;
		}

		internal FdbAsyncQuery(IFdbTransaction trans, FdbQueryExpression expression)
		{
			this.Transaction = trans;
			this.Expression = expression;
		}

		public FdbQueryExpression Expression { get; private set; }

		public FdbDatabase Database { get; private set; }

		public IFdbTransaction Transaction { get; private set; }

		public Type ElementType
		{
			get { return this.Expression.Type; }
		}

		IFdbAsyncQueryProvider IFdbAsyncQueryable.Provider
		{
			get { return this; }
		}

		public virtual IFdbAsyncQueryable CreateQuery(FdbQueryExpression expression)
		{
			if (expression == null) throw new ArgumentNullException("expression");

			return new FdbAsyncQuery(this.Database, expression);
		}

		public virtual IFdbAsyncQueryable<T> CreateQuery<T>(FdbQueryExpression<T> expression)
		{
			if (expression == null) throw new ArgumentNullException("expression");

			return new FdbAsyncQuery<T>(this.Database, expression);
		}

		public virtual IFdbAsyncSequenceQueryable<T> CreateSequenceQuery<T>(FdbQuerySequenceExpression<T> expression)
		{
			if (expression == null) throw new ArgumentNullException("expression");

			return new FdbAsyncSequenceQuery<T>(this.Database, expression);
		}

		Task<R> IFdbAsyncQueryProvider.Execute<R>(FdbQueryExpression expression, CancellationToken ct)
		{
			if (expression == null) throw new ArgumentNullException("ct");
			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<R>(ct);

			return ExecuteInternal<R>(expression, ct);
		}

		protected virtual Task<R> ExecuteInternal<R>(FdbQueryExpression expression, CancellationToken ct)
		{
			throw new NotImplementedException();
		}

	}

}
