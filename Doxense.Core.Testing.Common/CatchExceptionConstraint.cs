#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense
{
	using System;
	using System.Net;
	using System.Net.Http;
	using System.Net.Sockets;
	using System.Text;
	using Doxense.Serialization;
	using NUnit.Framework;
	using NUnit.Framework.Constraints;

	public class CatchExceptionConstraint<TException> : Constraint
		where TException : Exception
	{
		public sealed class Pokeball
		{
			private Exception? CapturedException { get; set; }

			public bool HasException => this.CapturedException != null;

			public TException Exception => (this.CapturedException ?? throw new AssertionException("No exception was captured!")) as TException ?? throw new AssertionException($"Captured exception was of type {this.CapturedException.GetType().GetFriendlyName()} instead of expected type {typeof(TException).GetFriendlyName()}");

			internal void Capture(Exception e) => this.CapturedException = e;

			public override string ToString()
			{
				if (this.CapturedException == null) return "<none>";
				var sb = new StringBuilder();
				var x = this.CapturedException;
				while (x != null)
				{
					if (sb.Length != 0) sb.Append(" -> ");
					sb.Append('[').Append(x.GetType().GetFriendlyName());
					switch (x)
					{
						case ArgumentException argEx:
							if (argEx.ParamName != null) sb.Append(':').Append(argEx.ParamName);
							break;
						case HttpRequestException httpEx:
							if (httpEx.StatusCode != null) sb.Append(':').Append(httpEx.StatusCode);
							break;
						case WebException webEx:
							sb.Append(':').Append(webEx.Status);
							break;
						case SocketException sockEx:
							sb.Append(':').Append(sockEx.SocketErrorCode);
							break;
					}
					sb.Append("] `").Append(x.Message).Append('`');
					x = x.InnerException;
				}
				return sb.ToString();
			}
		}

		public Pokeball Ball { get; }

		public CatchExceptionConstraint(Pokeball ball)
		{
			this.Ball = ball;
		}

		public override ConstraintResult ApplyTo<TActual>(TActual actual)
		{
			if (typeof(TActual) != typeof(Exception))
			{
				throw new InvalidOperationException($"Unexpected type {typeof(TActual).GetFriendlyName()} while expecting Exception result");
			}
			this.Ball.Capture((Exception) (object) actual);
			// success!
			return new ConstraintResult(this, this.Ball.Exception, true);
		}
	}

	public static class TestConstraintsExtensions
	{

		/// <summary>Permet de capturer l'exception interceptée et de l'exposer a la méthode de test</summary>
		/// <param name="constraint">Containte de type <c>Throws.InstanceOf</c></param>
		/// <param name="capture">Boite qui recevra l'exception capturée, après l'execution de l'assertion</param>
		public static CatchExceptionConstraint<Exception> Catch(this InstanceOfTypeConstraint constraint, out CatchExceptionConstraint<Exception>.Pokeball capture)
		{
			capture = new CatchExceptionConstraint<Exception>.Pokeball();
			var x = new CatchExceptionConstraint<Exception>(capture);
			constraint.Builder.Append(x);
			return x;
		}

		/// <summary>Permet de capturer l'exception interceptée et de l'exposer a la méthode de test</summary>
		/// <param name="constraint">Containte de type <c>Throws.InstanceOf</c></param>
		/// <param name="capture">Boite qui recevra l'exception capturée, après l'execution de l'assertion</param>
		public static CatchExceptionConstraint<TException> Catch<TException>(this InstanceOfTypeConstraint constraint, out CatchExceptionConstraint<TException>.Pokeball capture)
			where TException : Exception
		{
			capture = new CatchExceptionConstraint<TException>.Pokeball();
			var x = new CatchExceptionConstraint<TException>(capture);
			constraint.Builder.Append(x);
			return x;
		}

		/// <summary>Permet de capturer l'exception interceptée et de l'exposer a la méthode de test</summary>
		/// <param name="expr">Containte de type <c>Throws.Exception</c></param>
		/// <param name="capture">Boite qui recevra l'exception capturée, après l'execution de l'assertion</param>
		public static ResolvableConstraintExpression Catch(this ResolvableConstraintExpression expr, out CatchExceptionConstraint<Exception>.Pokeball capture)
		{
			capture = new CatchExceptionConstraint<Exception>.Pokeball();
			var x = new CatchExceptionConstraint<Exception>(capture);
			expr.Append(x);
			return expr;
		}

		/// <summary>Permet de capturer l'exception interceptée et de l'exposer a la méthode de test</summary>
		/// <param name="expr">Containte de type <c>Throws.Exception</c></param>
		/// <param name="capture">Boite qui recevra l'exception capturée, après l'execution de l'assertion</param>
		public static ResolvableConstraintExpression Catch<TException>(this ResolvableConstraintExpression expr, out CatchExceptionConstraint<TException>.Pokeball capture)
			where TException : Exception
		{
			capture = new CatchExceptionConstraint<TException>.Pokeball();
			var x = new CatchExceptionConstraint<TException>(capture);
			expr.Append(x);
			return expr;
		}
	}

}
