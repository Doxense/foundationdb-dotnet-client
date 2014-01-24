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
	using FoundationDB.Async;
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbExpressionHelpers
	{

		#region DebugView...

		private static readonly Func<Expression, string> DebugViewGetter = CreateDebugViewGetter();

		private static Func<Expression, string> CreateDebugViewGetter()
		{
			try
			{
				var prm = Expression.Parameter(typeof(Expression), "expr");
				var func = Expression.Lambda<Func<Expression, string>>(
					Expression.PropertyOrField(prm, "DebugView"),
					prm
				).Compile();

				// test if it really works
				func(Expression.Constant(123));

				return func;
			}
			catch
			{
				return (expr) => expr.ToString();
			}
		}

		/// <summary>Return the internal DebugView property of an expression</summary>
		/// <param name="expression">Expression</param>
		/// <returns>Debug string</returns>
		public static string GetDebugView(this Expression expression)
		{
			if (expression == null) return "<null>";
			return DebugViewGetter(expression);
		}

		#endregion

		public static T EvaluateConstantExpression<T>(Expression expression)
		{
			var expr = expression.CanReduce ? expression.Reduce() : expression;

			if (expr.Type != typeof(T)) throw new InvalidOperationException("Expression type mismatch");

			var constant = expr as ConstantExpression;
			if (constant != null) return (T)constant.Value;

			throw new NotSupportedException(String.Format("Unsupported expression {1}: '{0}' should return a constant value", expression.GetType().Name, expr.ToString()));
		}

		private static Task<T> Inline<T>(Func<IFdbReadOnlyTransaction, T> func, IFdbReadOnlyTransaction trans, CancellationToken ct)
		{
			try
			{
				if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<T>(ct);
				return Task.FromResult(func(trans));
			}
			catch(Exception e)
			{
				return TaskHelpers.FromException<T>(e);
			}
		}

		public static Expression<Func<IFdbReadOnlyTransaction, CancellationToken, Task<T>>> ToTask<T>(Expression<Func<IFdbReadOnlyTransaction, T>> lambda)
		{
			// rewrite a Func<..., T> into a Func<..., Task<T>>

			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");
			var prmCancel = Expression.Parameter(typeof(CancellationToken), "ct");

			var body = RewriteCall<Func<IFdbReadOnlyTransaction, CancellationToken, Func<IFdbReadOnlyTransaction, T>, Task<T>>>(
				(trans, ct, func) => Inline<T>(func, trans, ct),
				prmTrans,
				prmCancel,
				lambda
			);

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, CancellationToken, Task<T>>>(
				body,
				prmTrans,
				prmCancel
			);
		}

		internal static Task<R> ExecuteEnumerable<T, R>(Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>> generator, Func<IFdbAsyncEnumerable<T>, CancellationToken, Task<R>> lambda, IFdbReadOnlyTransaction trans, CancellationToken ct)
		{
			Contract.Requires(generator != null && lambda != null && trans != null);
			try
			{
				if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<R>(ct);
				var enumerable = generator(trans);
				if (enumerable == null) return TaskHelpers.FromException<R>(new InvalidOperationException("Source query returned null"));
				return lambda(enumerable, ct);
			}
			catch (Exception e)
			{
				return TaskHelpers.FromException<R>(e);
			}
		}

		internal static string GetOperatorAlias(ExpressionType op)
		{
			switch (op)
			{
				case ExpressionType.Equal: return "==";
				case ExpressionType.NotEqual: return "!=";
				case ExpressionType.GreaterThan: return ">";
				case ExpressionType.GreaterThanOrEqual: return ">=";
				case ExpressionType.LessThan: return "<";
				case ExpressionType.LessThanOrEqual: return "<=";

				case ExpressionType.Negate: return "-";
				case ExpressionType.Not: return "!";

				case ExpressionType.And: return "&";
				case ExpressionType.Or: return "|";

				case ExpressionType.AndAlso: return "&&";
				case ExpressionType.OrElse: return "||";

				case ExpressionType.Add: return "+";
				case ExpressionType.Subtract: return "-";
				case ExpressionType.Multiply: return "*";
				case ExpressionType.Divide: return "/";
				case ExpressionType.Modulo: return "%";

				default:
					return op.ToString();
			}
		}

		#region Method Call Rewritting...

		/// <summary>Rewrite a method call capture by a lambda, into the equivalent call where the parameters have been replaced by the actual values</summary>
		/// <typeparam name="TDelegate"></typeparam>
		/// <param name="lambda"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		/// <remarks>Typical use case is when we have a dummy Expression&lt;Func&lt;...&gt;&gt; used to get a method call signature, and we want to remplace the parameters of the lambda with the actual values that will be used at runtime</remarks>
		public static Expression RewriteCall<TDelegate>(Expression<TDelegate> lambda, params Expression[] arguments)
		{
			if (lambda == null) throw new ArgumentNullException("lambda");
			if (arguments.Length != lambda.Parameters.Count) throw new InvalidOperationException("Argument count mismatch");

			var rewritten = new Dictionary<ParameterExpression, Expression>(arguments.Length);
			for (int i = 0; i < arguments.Length;i++)
			{
				var arg = lambda.Parameters[i];
				if (!arg.Type.IsAssignableFrom(arguments[i].Type)) throw new InvalidOperationException(String.Format("Argument {0} of type {1} does not match type {2}", arg.Name, arg.Type.Name, arguments[i].Type.Name));
				rewritten[arg] = arguments[i];
			}

			return new ParameterRewritingVisitor(rewritten).Visit(lambda.Body);
		}

		/// <summary>Visitor that replace paramters of a lambda with other expressions</summary>
		private sealed class ParameterRewritingVisitor : ExpressionVisitor
		{

			public Dictionary<ParameterExpression, Expression> Parameters { get; private set; }

			public ParameterRewritingVisitor(Dictionary<ParameterExpression, Expression> rewrittenParameters)
			{
				Contract.Requires(rewrittenParameters != null && rewrittenParameters.Count > 0);
				this.Parameters = rewrittenParameters;
			}

			protected override Expression VisitParameter(ParameterExpression node)
			{

				Expression expr;
				if (this.Parameters.TryGetValue(node, out expr))
				{
					return expr;
				}
				return node;
			}

		}

		#endregion

	}

}
