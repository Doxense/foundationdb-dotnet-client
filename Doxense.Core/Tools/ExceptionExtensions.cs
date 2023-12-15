#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#nullable enable

namespace Doxense.Tools
{
	using Doxense.Diagnostics.Contracts;
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using JetBrains.Annotations;

	public static class ExceptionExtensions
	{
		private static readonly MethodInfo? s_preserveStackTrace = TryGetPreserveStackHandler();

		private static MethodInfo? TryGetPreserveStackHandler()
		{
			// Existe sous NETFX et aussi sous .NET Core (en tout cas au moins sous .NET Core 3.0)
			MethodInfo? handler = null;
			try
			{
				handler = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			catch { }
			Contract.Debug.Ensures(handler != null, "Exception.InternalPreserveStackTrace not found?");
			return handler;
		}

		/// <summary>Détermine s'il s'agit d'une erreur fatale (qu'il faudrait bouncer)</summary>
		/// <param name="self">Exception à tester</param>
		/// <returns>True s'il s'agit d'une ThreadAbortException, OutOfMemoryException ou StackOverflowException, ou une AggregateException qui contient une de ces erreurs</returns>
		[Pure]
		public static bool IsFatalError(
#if USE_ANNOTATIONS
			[System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
#endif
			this Exception? self
		)
		{
			return self is System.Threading.ThreadAbortException || self is OutOfMemoryException || self is StackOverflowException || (self is AggregateException && IsFatalError(self.InnerException));
		}

		/// <summary>Détermine s'il s'agit d'une erreur "interessante" pour le diagnostique de crash, et qui mérite d'écrire une stack-trace entière dans les logs</summary>
		/// <returns>Si true, il s'agit d'une erreur de type <see cref="NullReferenceException"/>, <see cref="ArgumentNullException"/>, <see cref="ArgumentOutOfRangeException"/>, <see cref="IndexOutOfRangeException"/> (ou similaire) qui justifie de tracer l'exception entière dans les logs.</returns>
		/// <remarks>Les erreurs "plus grave" (ex: <see cref="OutOfMemoryException"/>, <see cref="StackOverflowException"/>, ...) sont gérées par <see cref="IsFatalError"/>!)</remarks>
		public static bool IsLikelyBug(
#if USE_ANNOTATIONS
			[System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
#endif
			this Exception? self
		)
		{
			return self is NullReferenceException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException || (self is AggregateException && IsLikelyBug(self.InnerException));
		}

		/// <summary>Préserve la stacktrace lorsqu'on crée une exception, qui sera re-throwé plus haut</summary>
		/// <param name="self">Exception qui a été catchée</param>
		/// <returns>La même exception, mais avec la StackTrace préservée</returns>
		public static Exception PreserveStackTrace(this Exception self)
		{
			self = UnwrapIfAggregate(self);
			if (s_preserveStackTrace != null) s_preserveStackTrace.Invoke(self, null);
			return self;
		}

		/// <summary>Préserve la stacktrace lorsqu'on veut re-thrower une exception catchée</summary>
		/// <param name="self">Exception qui a été catchée</param>
		/// <returns>La même exception, mais avec la StackTrace préservée</returns>
		/// <remarks>Similaire à l'extension méthode PrepareForRethrow présente dans System.CoreEx.dll du Reactive Framework</remarks>
		public static Exception PrepForRemoting(this Exception self)
		{
			//TODO: cette extensions méthode est également présente dans System.CoreEx.dll du Reactive Framework!
			// il faudra peut etre a terme rerouter vers cette version (si un jour Sioux reférence Rx directement...)
			self = UnwrapIfAggregate(self);
			s_preserveStackTrace?.Invoke(self, null);
			return self;
		}

		/// <summary>Retourne la première exeception non-aggregate trouvée dans l'arbre des InnerExceptions</summary>
		/// <param name="self">AggregateException racine</param>
		/// <returns>Première exception dans l'arbre des InnerExceptions qui ne soit pas de type AggregateException</returns>
		public static Exception GetFirstConcreteException(this AggregateException self)
		{
			// dans la majorité des cas, on a une branche avec potentiellement plusieurs couches de AggEx mais une seule InnerException
			var e = self.GetBaseException();
			if (!(e is AggregateException)) return e;

			// Sinon c'est qu'on a un arbre a plusieurs branches, qu'on va devoir parcourir...
			var list = new Queue<AggregateException>();
			list.Enqueue(self);
			while (list.Count > 0)
			{
				foreach (var e2 in list.Dequeue().InnerExceptions)
				{
					if (e2 is null) continue;
					if (e2 is not AggregateException x) return e2; // on a trouvé une exception concrète !
					list.Enqueue(x);
				}
			}
			// uhoh ?
			return self;
		}

		/// <summary>Retourne la première exception non-aggregate si c'est une AggregateException, ou l'exception elle même dans les autres cas</summary>
		/// <param name="self"></param>
		/// <returns></returns>
		public static Exception UnwrapIfAggregate(this Exception self)
		{
			return self is AggregateException aggEx ? GetFirstConcreteException(aggEx) : self;
		}

		/// <summary>Rethrow la première exception non-aggregate trouvée, en jetant les autres s'il y en a</summary>
		/// <param name="self">AggregateException racine</param>
		[ContractAnnotation("self:null => null")]
#if USE_ANNOTATIONS
		[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull("self")]
#endif
		public static Exception? Unwrap(
			this AggregateException? self)
		{
			return self != null ? GetFirstConcreteException(self).PrepForRemoting() : null;
		}

		/// <summary>Unwrap generic exceptions like <see cref="AggregateException"/> or <see cref="TargetInvocationException"/> to return the inner exceptions</summary>
		public static Exception Unwrap(this Exception self)
		{
			if (self is AggregateException aggEx) return GetFirstConcreteException(aggEx);
			if (self is TargetInvocationException tiEx) return tiEx.InnerException ?? self;
			//add other type of "container" exceptions as required
			return self;
		}
	}

}
