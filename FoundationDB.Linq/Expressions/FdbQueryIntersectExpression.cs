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
	using System.Threading;

	/// <summary>Intersection between two or more sequence</summary>
	/// <typeparam name="T">Type of the keys returned</typeparam>
	public sealed class FdbQueryIntersectExpression<K, T> : FdbQuerySequenceExpression<T>
	{

		internal FdbQueryIntersectExpression(FdbQuerySequenceExpression<T>[] expressions, Expression<Func<T, K>> keySelector, IComparer<K> keyComparer)
		{
			this.Expressions = expressions;
			this.KeySelector = keySelector;
			this.KeyComparer = keyComparer;
		}

		public override FdbQueryNodeType NodeType
		{
			get { return FdbQueryNodeType.Intersect; }
		}

		internal FdbQuerySequenceExpression<T>[] Expressions { get; private set; }

		public IReadOnlyList<FdbQuerySequenceExpression<T>> Terms { get { return this.Expressions; } }

		public Expression<Func<T, K>> KeySelector { get; private set; }

		public IComparer<K> KeyComparer { get; private set; }

		public override Expression<Func<IFdbReadTransaction, IFdbAsyncEnumerable<T>>> CompileSequence(IFdbAsyncQueryProvider provider)
		{
			// compile the key selector
			var selector = this.KeySelector.Compile();

			var prmTrans = Expression.Parameter(typeof(IFdbReadTransaction), "trans");

			// get all inner enumerables
			var enumerables = new Expression[this.Expressions.Length];
			for(int i=0;i<this.Expressions.Length;i++)
			{
				enumerables[i] = Expression.Invoke(this.Expressions[i].CompileSequence(provider), prmTrans);
			}

			var array = Expression.NewArrayInit(typeof(IFdbAsyncEnumerable<T>), enumerables);
			var method = typeof(FdbMergeQueryExtensions).GetMethod("Intersect").MakeGenericMethod(typeof(T));

			return Expression.Lambda<Func<IFdbReadTransaction, IFdbAsyncEnumerable<T>>>(
				Expression.Call(method, array, Expression.Constant(selector), Expression.Constant(this.KeyComparer)),
				prmTrans
			);
		
		}

		internal override void AppendDebugStatement(FdbDebugStatementWriter writer)
		{
			writer.WriteLine("Intersect<{0}>(", this.ElementType.Name).Enter();
			writer.WriteLine("On({0}),", this.KeySelector.GetDebugView());
			writer.WriteLine("From([").Enter();
			for(int i=0;i<this.Expressions.Length;i++)
			{
				writer.Write(this.Expressions[i]);
				if (i + 1 < this.Expressions.Length)
					writer.WriteLine(",");
				else
					writer.WriteLine();
			}
			writer.Leave().WriteLine("])").Leave().Write(")");
		}

	}

}
