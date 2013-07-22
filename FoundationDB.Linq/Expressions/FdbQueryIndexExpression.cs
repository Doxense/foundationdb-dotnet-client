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

#if REFACTORED

namespace FoundationDB.Linq.Expressions
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Linq.Utils;
	using System;
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Wrapper on an FdbIndex instance</summary>
	/// <typeparam name="TId">Type of the Id of entities being indexed</typeparam>
	/// <typeparam name="TValue">Type of the value of property being indexed for each entity</typeparam>
	public sealed class FdbQueryIndexExpression<TId, TValue> : FdbQueryExpression<FdbIndex<TId, TValue>>
	{

		internal FdbQueryIndexExpression(FdbIndex<TId, TValue> index)
		{
			this.Index = index;
		}

		public override FdbQueryNodeType NodeType
		{
			get { return FdbQueryNodeType.IndexName; }
		}

		public FdbIndex<TId, TValue> Index { get; private set; }

		public Type KeyType { get { return typeof(TId); } }

		public Type ValueType { get { return typeof(TValue); } }

		public override Expression<Func<IFdbReadTransaction, CancellationToken, Task<FdbIndex<TId, TValue>>>> CompileSingle(IFdbAsyncQueryProvider provider)
		{
			return Expression.Lambda<Func<IFdbReadTransaction, CancellationToken, Task<FdbIndex<TId, TValue>>>>(
				Expression.Constant(this.Index),
				Expression.Parameter(typeof(IFdbReadTransaction)),
				Expression.Parameter(typeof(CancellationToken))
			);
		}

		internal override void AppendDebugStatement(FdbDebugStatementWriter writer)
		{
			writer.Write("Index[{0}]", this.Index.Name);
		}

	}

}

#endif