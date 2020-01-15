#region BSD License
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
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Linq;
	using FoundationDB.Client;

	/// <summary>Base class of all queries that return a sequence of elements (Ranges, Index lookups, ...)</summary>
	/// <typeparam name="T">Type of items returned</typeparam>
	public abstract class FdbQuerySequenceExpression<T> : FdbQueryExpression<IAsyncEnumerable<T>>
	{
		/// <summary>Type of elements returned by the sequence</summary>
		public Type ElementType
		{
			[NotNull]
			get { return typeof(T); }
		}

		/// <summary>Always returns <see cref="FdbQueryShape.Sequence"/></summary>
		public override FdbQueryShape Shape
		{
			get { return FdbQueryShape.Sequence; }
		}

		/// <summary>Returns a new expression that creates an async sequence that will execute this query on a transaction</summary>
		public abstract Expression<Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>>> CompileSequence();

		/// <summary>Returns a new expression that creates an async sequence that will execute this query on a transaction</summary>
		public override Expression<Func<IFdbReadOnlyTransaction, CancellationToken, Task<IAsyncEnumerable<T>>>> CompileSingle()
		{
			//REVIEW: why is it called CompileSingle ??
			return FdbExpressionHelpers.ToTask(CompileSequence());
		}

	}

}
