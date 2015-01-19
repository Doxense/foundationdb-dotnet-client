﻿#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;

	/// <summary>Mode of execution of a merge operation</summary>
	public enum FdbQueryMergeType
	{
		/// <summary>Only returns the elements that are present in all the source sequences.</summary>
		Intersect,
		/// <summary>Return all the elements that are in all the source sequences, but only once.</summary>
		Union,
		/// <summary>Return only the elements that are in the first source sequence, but not in any of the other sequences.</summary>
		Except,
	}

	/// <summary>Intersection between two or more sequence</summary>
	/// <typeparam name="T">Type of the keys returned</typeparam>
	public abstract class FdbQueryMergeExpression<T> : FdbQuerySequenceExpression<T>
	{

		/// <summary>Create a new merge expression</summary>
		protected FdbQueryMergeExpression(FdbQuerySequenceExpression<T>[] expressions, IComparer<T> keyComparer)
		{
			Contract.Requires(expressions != null);
			this.Expressions = expressions;
			this.KeyComparer = keyComparer;
		}

		/// <summary>Type of the merge applied to the source sequences</summary>
		public abstract FdbQueryMergeType MergeType { get; }

		internal FdbQuerySequenceExpression<T>[] Expressions
		{
			[NotNull] get;
			private set;
		}

		/// <summary>Liste of the source sequences that are being merged</summary>
		public IReadOnlyList<FdbQuerySequenceExpression<T>> Terms
		{
			[NotNull]
			get { return this.Expressions; }
		}

		/// <summary>Comparer used during merging</summary>
		public IComparer<T> KeyComparer
		{
			[CanBeNull] get;
			private set;
		}

		/// <summary>Apply a custom visitor to this expression</summary>
		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQueryMerge(this);
		}

		/// <summary>Write a human-readable explanation of this expression</summary>
		public override void WriteTo(FdbQueryExpressionStringBuilder builder)
		{
			builder.Writer.WriteLine("{0}<{1}>(", this.MergeType, this.ElementType.Name).Enter();
			for (int i = 0; i < this.Expressions.Length; i++)
			{
				builder.Visit(this.Expressions[i]);
				if (i + 1 < this.Expressions.Length)
					builder.Writer.WriteLine(",");
				else
					builder.Writer.WriteLine();
			}
			builder.Writer.Leave().Write(")");
		}

		/// <summary>Returns a new expression that creates an async sequence that will execute this query on a transaction</summary>
		[NotNull]
		public override Expression<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>>> CompileSequence()
		{
			// compile the key selector
			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			// get all inner enumerables
			var enumerables = new Expression[this.Expressions.Length];
			for(int i=0;i<this.Expressions.Length;i++)
			{
				enumerables[i] = FdbExpressionHelpers.RewriteCall(this.Expressions[i].CompileSequence(), prmTrans);
			}

			var array = Expression.NewArrayInit(typeof(IFdbAsyncEnumerable<T>), enumerables);
			Expression body;
			switch (this.MergeType)
			{
				case FdbQueryMergeType.Intersect:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<IFdbAsyncEnumerable<T>[], IComparer<T>, IFdbAsyncEnumerable<T>>>(
						(sources, comparer) => FdbMergeQueryExtensions.Intersect(sources, comparer),
						array,
						Expression.Constant(this.KeyComparer, typeof(IComparer<T>))
					);
					break;
				}
				case FdbQueryMergeType.Union:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<IFdbAsyncEnumerable<T>[], IComparer<T>, IFdbAsyncEnumerable<T>>>(
						(sources, comparer) => FdbMergeQueryExtensions.Union(sources, comparer),
						array,
						Expression.Constant(this.KeyComparer, typeof(IComparer<T>))
					);
					break;
				}
				default:
				{
					throw new InvalidOperationException(String.Format("Unsupported index merge mode {0}", this.MergeType.ToString()));
				}
			}

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>>>(
				body,
				prmTrans
			);
		
		}

	}

	/// <summary>Intersection between two or more sequence</summary>
	/// <typeparam name="T">Type of the keys returned</typeparam>
	public sealed class FdbQueryIntersectExpression<T> : FdbQueryMergeExpression<T>
	{

		internal FdbQueryIntersectExpression(FdbQuerySequenceExpression<T>[] expressions, IComparer<T> keyComparer)
			: base(expressions, keyComparer)
		{ }

		/// <summary>Returns <see cref="FdbQueryMergeType.Intersect"/></summary>
		public override FdbQueryMergeType MergeType
		{
			get { return FdbQueryMergeType.Intersect; }
		}

	}

	/// <summary>Union between two or more sequence</summary>
	/// <typeparam name="T">Type of the keys returned</typeparam>
	public sealed class FdbQueryUnionExpression<T> : FdbQueryMergeExpression<T>
	{

		internal FdbQueryUnionExpression(FdbQuerySequenceExpression<T>[] expressions, IComparer<T> keyComparer)
			: base(expressions, keyComparer)
		{ }

		/// <summary>Returns <see cref="FdbQueryMergeType.Union"/></summary>
		public override FdbQueryMergeType MergeType
		{
			get { return FdbQueryMergeType.Union; }
		}

	}

}
