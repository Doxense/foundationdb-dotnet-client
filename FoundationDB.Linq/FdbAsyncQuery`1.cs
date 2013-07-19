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
	using FoundationDB.Client;
	using FoundationDB.Linq.Expressions;
	using FoundationDB.Linq.Utils;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbAsyncQuery<T> : FdbAsyncQuery, IFdbAsyncQueryable<T>, IFdbAsyncQueryProvider<T>
	{
		public FdbAsyncQuery(FdbDatabase db, FdbQueryExpression<T> expression)
			: base(db, expression)
		{ }

		public new FdbQueryExpression<T> Expression { get { return (FdbQueryExpression<T>)base.Expression; } }

		IFdbAsyncQueryProvider<T> IFdbAsyncQueryable<T>.Provider { get { return this; } }

		/// <summary>Cached compiled generator, that can be reused</summary>
		private Func<IFdbReadTransaction, CancellationToken, Task<T>> m_compiled;

		private Func<IFdbReadTransaction, CancellationToken, Task<T>> Compile()
		{
			if (m_compiled == null)
			{
				var expr = this.Expression.CompileSingle(this);
				Console.WriteLine("Compiled as: " + expr.GetDebugView());
				m_compiled = expr.Compile();
			}
			return m_compiled;
		}

		Task<T> IFdbAsyncQueryProvider<T>.ExecuteSingle(FdbQueryExpression<T> expression, CancellationToken ct)
		{
			return ExecuteInternal<T>(expression, ct);
		}

		protected override async Task<R> ExecuteInternal<R>(FdbQueryExpression expression, CancellationToken ct)
		{
			if (typeof(R) != typeof(T)) throw new InvalidOperationException("Return type does not match the sequence");

			var generator = Compile();

			IFdbTransaction trans = this.Transaction;
			bool owned = false;
			try
			{
				if (trans == null)
				{
					owned = true;
					trans = this.Database.BeginTransaction();
				}

				T result = await generator(trans, ct).ConfigureAwait(false);

				return (R)(object)result;

			}
			finally
			{
				if (owned && trans != null) trans.Dispose();
			}

		}
	}

}
