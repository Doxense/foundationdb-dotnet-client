#region Copyright Doxense 2005-2015
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB
{
	using Doxense.Diagnostics.Contracts;
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using JetBrains.Annotations;

	internal static class ExceptionExtensions
	{
		private static readonly MethodInfo s_preserveStackTrace;
		private static readonly MethodInfo s_prepForRemoting;

		static ExceptionExtensions()
		{
			try
			{
				s_preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
				s_prepForRemoting = typeof(Exception).GetMethod("PrepForRemoting", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			catch { }
			Contract.Ensures(s_preserveStackTrace != null, "Exception.InternalPreserveStackTrace not found?");
			Contract.Ensures(s_prepForRemoting != null, "Exception.PrepForRemoting not found?");
		}

		/// <summary>Détermine s'il s'agit d'une erreur fatale (qu'il faudrait bouncer)</summary>
		/// <param name="self">Exception à tester</param>
		/// <returns>True s'il s'agit d'une ThreadAbortException, OutOfMemoryException ou StackOverflowException, ou une AggregateException qui contient une de ces erreurs</returns>
		[Pure]
		public static bool IsFatalError([CanBeNull] this Exception self)
		{
			return self is System.Threading.ThreadAbortException || self is OutOfMemoryException || self is StackOverflowException || (self is AggregateException && IsFatalError(self.InnerException));
		}

		/// <summary>Préserve la stacktrace lorsqu'on crée une exception, qui sera re-throwé plus haut</summary>
		/// <param name="self">Exception qui a été catchée</param>
		/// <returns>La même exception, mais avec la StackTrace préservée</returns>
		[NotNull]
		public static Exception PreserveStackTrace([NotNull] this Exception self)
		{
			self = UnwrapIfAggregate(self);
			if (s_preserveStackTrace != null) s_preserveStackTrace.Invoke(self, null);
			return self;
		}

		/// <summary>Préserve la stacktrace lorsqu'on veut re-thrower une exception catchée</summary>
		/// <param name="self">Exception qui a été catchée</param>
		/// <returns>La même exception, mais avec la StackTrace préservée</returns>
		/// <remarks>Similaire à l'extension méthode PrepareForRethrow présente dans System.CoreEx.dll du Reactive Framework</remarks>
		[NotNull]
		public static Exception PrepForRemoting([NotNull] this Exception self)
		{
			//TODO: cette extensions méthode est également présente dans System.CoreEx.dll du Reactive Framework!
			// il faudra peut etre a terme rerouter vers cette version (si un jour Sioux reférence Rx directement...)
			self = UnwrapIfAggregate(self);
			if (s_prepForRemoting != null) s_prepForRemoting.Invoke(self, null);
			return self;
		}

		/// <summary>Retourne la première exeception non-aggregate trouvée dans l'arbre des InnerExceptions</summary>
		/// <param name="self">AggregateException racine</param>
		/// <returns>Première exception dans l'arbre des InnerExceptions qui ne soit pas de type AggregateException</returns>
		[NotNull]
		public static Exception GetFirstConcreteException([NotNull] this AggregateException self)
		{
			// dans la majorité des cas, on a une branche avec potentiellement plusieurs couches de AggEx mais une seule InnerException
			var e = self.GetBaseException();
			if (!(e is AggregateException)) return e;

			// Sinon c'est qu'on a un arbre a plusieures branches, qu'on va devoir parcourir...
			var list = new Queue<AggregateException>();
			list.Enqueue(self);
			while (list.Count > 0)
			{
				foreach (var e2 in list.Dequeue().InnerExceptions)
				{
					if (e2 == null) continue;
					if (!(e2 is AggregateException x)) return e2; // on a trouvé une exception concrète !
					list.Enqueue(x);
				}
			}
			// uhoh ?
			return self;
		}

		/// <summary>Retourne la première exception non-aggregate si c'est une AggregateException, ou l'exception elle même dans les autres cas</summary>
		/// <param name="self"></param>
		/// <returns></returns>
		[NotNull]
		public static Exception UnwrapIfAggregate([NotNull] this Exception self)
		{
			return self is AggregateException aggEx ? GetFirstConcreteException(aggEx) : self;
		}

		/// <summary>Rethrow la première exception non-aggregate trouvée, en jettant les autres s'il y en a</summary>
		/// <param name="self">AggregateException racine</param>
		[ContractAnnotation("self:null => null")]
		public static Exception Unwrap(this AggregateException self)
		{
			return self != null ? GetFirstConcreteException(self).PrepForRemoting() : null;
		}

		/// <summary>Unwrap generic exceptions like <see cref="AggregateException"/> or <see cref="TargetInvocationException"/> to return the inner exceptions</summary>
		[NotNull]
		public static Exception Unwrap([NotNull] this Exception self)
		{
			if (self is AggregateException aggEx) return GetFirstConcreteException(aggEx);
			if (self is TargetInvocationException tiEx) return tiEx.InnerException ?? self;
			//add other type of "container" exceptions as required
			return self;
		}
	}

}
