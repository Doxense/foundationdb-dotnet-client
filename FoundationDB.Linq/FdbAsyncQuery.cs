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

	public abstract class FdbAsyncQuery<T> : IFdbAsyncQueryable, IFdbAsyncQueryProvider
	{

		protected FdbAsyncQuery(FdbDatabase db)
		{
			this.Database = db;
		}

		protected FdbAsyncQuery(IFdbTransaction trans)
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

			return new FdbAsyncSingleQuery<R>(this.Database, expression);
		}

		public virtual IFdbAsyncSequenceQueryable<R> CreateSequenceQuery<R>(FdbQuerySequenceExpression<R> expression)
		{
			if (expression == null) throw new ArgumentNullException("expression");

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

		/// <summary>Cached compiled generator, that can be reused</summary>
		private Func<IFdbReadTransaction, CancellationToken, Task<T>> m_compiledSingle;

		private Func<IFdbReadTransaction, CancellationToken, Task<T>> CompileSingle()
		{
			if (m_compiledSingle == null)
			{
				var expr = ((FdbQueryExpression<T>)this.Expression).CompileSingle(this);
				//Console.WriteLine("Compiled single as:");
				//Console.WriteLine("> " + expr.GetDebugView().Replace("\r\n", "\r\n> "));
				m_compiledSingle = expr.Compile();
			}
			return m_compiledSingle;
		}

		protected virtual async Task<object> ExecuteSingleInternal(FdbQueryExpression expression, Type resultType, CancellationToken ct)
		{
			var generator = CompileSingle();

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

				return result;

			}
			finally
			{
				if (owned && trans != null) trans.Dispose();
			}

		}

		#endregion

		#region Sequence...

		/// <summary>Cached compiled generator, that can be reused</summary>
		private Func<IFdbReadTransaction, IFdbAsyncEnumerable<T>> m_compiledSequence;

		private Func<IFdbReadTransaction, IFdbAsyncEnumerable<T>> CompileSequence()
		{
			if (m_compiledSequence == null)
			{
				var expr = (this.Expression as FdbQuerySequenceExpression<T>).CompileSequence(this);
				//Console.WriteLine("Compiled sequence as:");
				//Console.WriteLine("> " + expr.GetDebugView().Replace("\r\n", "\r\n> "));
				m_compiledSequence = expr.Compile();
			}
			return m_compiledSequence;
		}

		public IFdbAsyncEnumerable<T> ToEnumerable()
		{
			return FdbAsyncEnumerable.Create((state) => GetEnumerator((FdbAsyncSequenceQuery<T>)state));
		}

		internal static IFdbAsyncEnumerator<T> GetEnumerator(FdbAsyncSequenceQuery<T> sequence)
		{
			var generator = sequence.CompileSequence();

			if (sequence.Transaction != null)
			{
				return generator(sequence.Transaction).GetEnumerator();
			}

			IFdbTransaction trans = null;
			IFdbAsyncEnumerator<T> iterator = null;
			bool success = true;
			try
			{
				trans = sequence.Database.BeginTransaction();
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
			var generator = CompileSequence();

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
