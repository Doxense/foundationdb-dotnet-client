﻿#region BSD Licence
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

namespace FoundationDB.Linq.Providers
{
	using FoundationDB.Client;
	using FoundationDB.Linq.Expressions;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public abstract class FdbAsyncQuery<T> : IFdbAsyncQueryable, IFdbAsyncQueryProvider
	{

		protected FdbAsyncQuery(IFdbDatabase db, FdbQueryExpression expression = null)
		{
			this.Database = db;
			this.Expression = expression;
		}

		protected FdbAsyncQuery(IFdbReadOnlyTransaction trans, FdbQueryExpression expression = null)
		{
			this.Transaction = trans;
			this.Expression = expression;
		}

		public FdbQueryExpression Expression { get; private set; }

		public IFdbDatabase Database { get; private set; }

		public IFdbReadOnlyTransaction Transaction { get; private set; }

		public virtual Type Type { get { return this.Expression.Type; } }

		IFdbAsyncQueryProvider IFdbAsyncQueryable.Provider
		{
			get { return this; }
		}

		public virtual IFdbAsyncQueryable CreateQuery(FdbQueryExpression expression)
		{
			// source queries are usually only intended to produce some sort of result
			throw new NotSupportedException();
		}

		public virtual IFdbAsyncQueryable<R> CreateQuery<R>(FdbQueryExpression<R> expression)
		{
			if (expression == null) throw new ArgumentNullException("expression");

			if (this.Transaction != null)
				return new FdbAsyncSingleQuery<R>(this.Transaction, expression);
			else
				return new FdbAsyncSingleQuery<R>(this.Database, expression);
		}

		public virtual IFdbAsyncSequenceQueryable<R> CreateSequenceQuery<R>(FdbQuerySequenceExpression<R> expression)
		{
			if (expression == null) throw new ArgumentNullException("expression");

			if (this.Transaction != null)
				return new FdbAsyncSequenceQuery<R>(this.Transaction, expression);
			else
				return new FdbAsyncSequenceQuery<R>(this.Database, expression);
		}

		public async Task<R> ExecuteAsync<R>(FdbQueryExpression expression, CancellationToken ct)
		{
			if (expression == null) throw new ArgumentNullException("ct");
			ct.ThrowIfCancellationRequested();

			var result = await ExecuteInternal(expression, typeof(R), ct).ConfigureAwait(false);
			return (R)result;
		}

		protected virtual Task<object> ExecuteInternal(FdbQueryExpression expression, Type resultType, CancellationToken ct)
		{
			switch(expression.Shape)
			{
				case FdbQueryShape.Single:
				{
					if (!expression.Type.IsAssignableFrom(resultType)) throw new InvalidOperationException(String.Format("Return type {0} does not match the sequence type {1}", resultType.Name, expression.Type.Name));
					return ExecuteSingleInternal(expression, resultType, ct);
				}

				case FdbQueryShape.Sequence:
					return ExecuteSequenceInternal(expression, resultType, ct);

				case FdbQueryShape.Void:
					return Task.FromResult(default(object));

				default:
					throw new InvalidOperationException("Invalid sequence shape");
			}
		}

		#region Single...

		protected Func<IFdbReadOnlyTransaction, CancellationToken, Task<T>> CompileSingle(FdbQueryExpression expression)
		{
			//TODO: caching !

			var expr = ((FdbQueryExpression<T>)expression).CompileSingle();
			//Console.WriteLine("Compiled single as:");
			//Console.WriteLine("> " + expr.GetDebugView().Replace("\r\n", "\r\n> "));
			return expr.Compile();
		}

		protected virtual async Task<object> ExecuteSingleInternal(FdbQueryExpression expression, Type resultType, CancellationToken ct)
		{
			var generator = CompileSingle(expression);

			var trans = this.Transaction;
			bool owned = false;
			try
			{
				if (trans == null)
				{
					owned = true;
					trans = this.Database.BeginTransaction(ct);
				}

				T result = await generator(trans, ct).ConfigureAwait(false);

				return result;

			}
			finally
			{
				if (owned && trans != null) trans.Dispose();
			}

		}

		#endregion

		#region Sequence...

		private Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>> CompileSequence(FdbQueryExpression expression)
		{
			//TODO: caching !
			Console.WriteLine("Source expression:");
			Console.WriteLine("> " + expression.GetDebugView().Replace("\r\n", "\r\n> "));

			var expr = ((FdbQuerySequenceExpression<T>) expression).CompileSequence();
			Console.WriteLine("Compiled sequence as:");
			Console.WriteLine("> " + expr.GetDebugView().Replace("\r\n", "\r\n> "));
			return expr.Compile();
		}

		internal static IFdbAsyncEnumerator<T> GetEnumerator(FdbAsyncSequenceQuery<T> sequence, FdbAsyncMode mode)
		{
			var generator = sequence.CompileSequence(sequence.Expression);

			if (sequence.Transaction != null)
			{
				return generator(sequence.Transaction).GetEnumerator(mode);
			}

			//BUGBUG: how do we get a CancellationToken without a transaction?
			var ct = CancellationToken.None;

			IFdbTransaction trans = null;
			IFdbAsyncEnumerator<T> iterator = null;
			bool success = true;
			try
			{
				trans = sequence.Database.BeginTransaction(ct);
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

		private sealed class TransactionIterator : IFdbAsyncEnumerator<T>
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

		protected virtual async Task<object> ExecuteSequenceInternal(FdbQueryExpression expression, Type resultType, CancellationToken ct)
		{
			var generator = CompileSequence(expression);

			var trans = this.Transaction;
			bool owned = false;
			try
			{
				if (trans == null)
				{
					owned = true;
					trans = this.Database.BeginTransaction(ct);
				}

				var enumerable = generator(trans);

				object result;

				if (typeof(T[]).IsAssignableFrom(resultType))
				{
					result = await enumerable.ToArrayAsync(ct).ConfigureAwait(false);
				}
				else if (typeof(IEnumerable<T>).IsAssignableFrom(resultType))
				{
					result = await enumerable.ToListAsync(ct).ConfigureAwait(false);
				}
				else
				{
					throw new InvalidOperationException(String.Format("Sequence result type {0} is not supported", resultType.Name));
				}

				return result;
			}
			finally
			{
				if (owned && trans != null) trans.Dispose();
			}
		}

		#endregion

	}

}
