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
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Async LINQ query that returns an async sequence of items</summary>
	/// <typeparam name="T">Type of the items in the sequence</typeparam>
	public sealed class FdbAsyncSequenceQuery<T> : FdbAsyncQuery<IFdbAsyncEnumerable<T>>, IFdbAsyncSequenceQueryable<T>, IFdbAsyncSequenceQueryProvider<T>, IFdbAsyncEnumerable<T>
	{

		public FdbAsyncSequenceQuery(FdbDatabase db, FdbQuerySequenceExpression<T> expression)
			: base(db, expression)
		{ }

		public new FdbQuerySequenceExpression<T> Expression { get { return (FdbQuerySequenceExpression<T>)base.Expression; } }

		/// <summary>Cached compiled generator, that can be reused</summary>
		private Func<IFdbReadTransaction, IFdbAsyncEnumerable<T>> m_compiled;

		private Func<IFdbReadTransaction, IFdbAsyncEnumerable<T>> Compile()
		{
			if (m_compiled == null)
			{
				var expr = this.Expression.CompileSequence(this);
				Console.WriteLine("Compiled as:");
				Console.WriteLine("> " + expr.GetDebugView().Replace("\r\n", "\r\n> "));
				m_compiled = expr.Compile();
			}
			return m_compiled;
		}

		public IFdbAsyncEnumerator<T> GetEnumerator()
		{
			var generator = Compile();

			if (this.Transaction != null)
			{
				return generator(this.Transaction).GetEnumerator();
			}

			IFdbTransaction trans = null;
			IFdbAsyncEnumerator<T> iterator = null;
			bool success = true;
			try
			{
				trans = this.Database.BeginTransaction();
				iterator = generator(trans).GetEnumerator();

				return new TransactionIterator(trans, iterator);
			}
			catch (Exception)
			{
				success = false;
				throw;
			}
			finally
			{
				if (!success)
				{
					if (iterator != null) iterator.Dispose();
					if (trans != null) trans.Dispose();
				}
			}
		}

		private class TransactionIterator : IFdbAsyncEnumerator<T>
		{
			private readonly IFdbAsyncEnumerator<T> m_iterator;
			private readonly IFdbTransaction m_transaction;

			public TransactionIterator(IFdbTransaction transaction, IFdbAsyncEnumerator<T> iterator)
			{
				m_transaction = transaction;
				m_iterator = iterator;
			}

			public Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				return m_iterator.MoveNext(cancellationToken);
			}

			public T Current
			{
				get { return m_iterator.Current; }
			}

			public void Dispose()
			{
				try
				{
					m_iterator.Dispose();
				}
				finally
				{
					m_transaction.Dispose();
				}
			}
		}

		Task<TSequence> IFdbAsyncSequenceQueryProvider<T>.ExecuteSequence<TSequence>(FdbQuerySequenceExpression<T> expression, CancellationToken ct)
		{
			return ExecuteInternal<TSequence>(expression, ct);
		}

		protected override async Task<R> ExecuteInternal<R>(FdbQueryExpression expression, CancellationToken ct)
		{
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

				var enumerable = generator(trans);

				object result;

				if (typeof(R).IsInstanceOfType(typeof(T[])))
				{
					result = await enumerable.ToArrayAsync(ct).ConfigureAwait(false);
				}
				else if (typeof(R).IsInstanceOfType(typeof(ICollection<T>)))
				{
					result = await enumerable.ToListAsync(ct).ConfigureAwait(false);
				}
				else
				{
					throw new InvalidOperationException("Sequence result type is not supported");
				}

				return (R)result;
			}
			finally
			{
				if (owned && trans != null) trans.Dispose();
			}
		}

	}

}
