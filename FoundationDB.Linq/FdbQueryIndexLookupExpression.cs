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
	using System;
	using System.Linq.Expressions;

	/// <summary>Expression that represents a lookup on an FdbIndex</summary>
	/// <typeparam name="TId">Type of the Id of the enties being indexed</typeparam>
	/// <typeparam name="TValue">Type of the value that will be looked up</typeparam>
	public class FdbQueryIndexLookupExpression<TId, TValue> : FdbQuerySequenceExpression<TId>
	{

		internal FdbQueryIndexLookupExpression(FdbQueryIndexExpression<TId, TValue> index, ExpressionType op, Expression value)
		{
			this.Index = index;
			this.Operator = op;
			this.Value = value;
		}

		public override FdbQueryNodeType NodeType
		{
			get { return FdbQueryNodeType.IndexLookup; }
		}

		public FdbQueryIndexExpression<TId, TValue> Index { get; private set; }

		public ExpressionType Operator { get; private set; }

		public Expression Value { get; private set; }

		internal override void AppendDebugStatement(FdbDebugStatementWriter writer)
		{
			writer
				.Write(this.Index)
				.Write(".Lookup<{0}>({1}, <{2}> {3})", this.ElementType.Name, this.Operator.ToString(), typeof(TValue).Name, this.Value.ToString());
		}

	}

}
