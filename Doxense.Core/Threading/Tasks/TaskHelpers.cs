#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace Doxense.Threading.Tasks
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Tools;
	using JetBrains.Annotations;

	/// <summary>Helper methods to work on tasks</summary>
	public static class TaskHelpers
	{

		#region .NET 4.5 async emulator

		#region Task.Run(Action, ...) ...

		/// <summary>Options par défaut de création de Task pour Task.Run(...)</summary>
		/// <remarks>En .NET 4.5 on utilise TaskCreationOptions.DenyChildAttach. En .NET 4.0 on utilise TaskCreationOptions.None</remarks>
		private const TaskCreationOptions DefaultTaskCreationOptions = TaskCreationOptions.DenyChildAttach;

		#endregion

		#region Task.Run(Func<..>, ...)

		public static Task<TResult> Run<TResult>(Func<TResult> function, CancellationToken ct, TaskScheduler scheduler)
		{
			return Task.Factory.StartNew(
				function,
				ct,
				DefaultTaskCreationOptions,
				scheduler ?? TaskScheduler.Default
			);
		}

		public static Task<TResult> Run<TArg0, TResult>(Func<TArg0, TResult> function, TArg0 arg0, CancellationToken ct, TaskScheduler? taskScheduler = null)
		{
			return RunPromise<TArg0, TResult>.Run(function, arg0, ct, taskScheduler);
		}

		#endregion

		#region Task.Run(Func<Task<..>>, ...)

		public static Task<TResult> Run<TResult>(Func<Task<TResult>> asyncContinuation, CancellationToken ct, TaskScheduler taskScheduler)
		{
			// note: on copie l'implémentation de Task.Run(...), a la seule différence que le Unwrap(...) manuel laisser passer les OperationCanceledException en tempts que status Faulted.
			return Task<Task<TResult>>.Factory.StartNew(asyncContinuation, ct, TaskCreationOptions.DenyChildAttach, taskScheduler).Unwrap();
		}

		public static Task<TResult> Run<TArg0, TResult>(Func<TArg0, Task<TResult>> asyncContinuation, TArg0 arg0, CancellationToken ct, TaskScheduler? taskScheduler = null)
		{
			return RunPromise<TArg0, Task<TResult>>.Run(asyncContinuation, arg0, ct, taskScheduler).FastUnwrap();
		}

		#endregion

		#region WithCancellation...

		/// <summary>Wraps a classic lambda into one that supports cancellation</summary>
		/// <param name="lambda">Lambda that does not support cancellation</param>
		/// <returns>New lambda that will check if the token is cancelled before calling <paramref name="lambda"/></returns>
		public static Func<TSource, CancellationToken, TResult> WithCancellation<TSource, TResult>(Func<TSource, TResult> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			return (value, ct) =>
			{
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
				return lambda(value);
			};
		}

		/// <summary>Wraps a classic lambda into one that supports cancellation</summary>
		/// <param name="lambda">Lambda that does not support cancellation</param>
		/// <returns>New lambda that will check if the token is cancelled before calling <paramref name="lambda"/></returns>
		public static Func<TSource, CancellationToken, Task<TResult>> WithCancellation<TSource, TResult>(Func<TSource, Task<TResult>> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			return (value, ct) =>
			{
				if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);
				return lambda(value);
			};
		}

		#endregion

		#region TaskCompletionSource Helpers...

		/// <summary>Propage le résultat d'une Task&lt;T&gt; terminée vers une TaskCompletionSource</summary>
		/// <typeparam name="T">Type du résultat de la Task</typeparam>
		/// <param name="self">TaskCompletionSource sur lequel dupliquer l'état de la Task</param>
		/// <param name="completedTask">Task qui doit etre terminée (RanToCompletion/Cancelled/Faulted)</param>
		/// <exception cref="System.InvalidOperationException">Si la task n'est pas dans un état terminée</exception>
		public static void PropagateResult<T>(this TaskCompletionSource<T> self, Task<T> completedTask)
		{
			Contract.NotNull(self);
			Contract.NotNull(completedTask);

			switch (completedTask.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					self.TrySetResult(completedTask.Result);
					break;
				}
				case TaskStatus.Faulted:
				{
					self.TrySetException(completedTask.Exception.InnerExceptions);
					break;
				}
				case TaskStatus.Canceled:
				{
					self.TrySetCanceled();
					break;
				}
				default:
				{
					throw new InvalidOperationException("Task was not completed");
				}
			}
		}

		/// <summary>Propage l'état d'une Task terminée vers une TaskCompletionSource</summary>
		/// <typeparam name="T">Type du résultat attendu par la TaskCompletionSource</typeparam>
		/// <param name="self">Source de completion à déclencher</param>
		/// <param name="completedTask">Tâche terminée, dont l'état sera recopié sur la TaskCompletionSource</param>
		/// <param name="result">Valeur a assigner à la completionSource si la tâche a réussi</param>
		/// <remarks>Si la task est annulée, la source sera annulée. Si la task a échoué, les exceptions seront copiées. Si la tâche a réussie, la valeur 'result' sera assignée à la source</remarks>
		public static void PropagateStatus<T>(this TaskCompletionSource<T> self, Task completedTask, T result = default(T))
		{
			Contract.NotNull(self);
			Contract.NotNull(completedTask);

			switch (completedTask.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					self.TrySetResult(result);
					break;
				}
				case TaskStatus.Faulted:
				{
					self.TrySetException(completedTask.Exception.InnerExceptions);
					break;
				}
				case TaskStatus.Canceled:
				{
					self.TrySetCanceled();
					break;
				}
				default:
				{
					throw new InvalidOperationException("Task was not completed");
				}
			}
		}

		/// <summary>Link l'état final d'une task avec un TaskCompletionSource (qui sera déclenché en même temps que la Task)</summary>
		/// <typeparam name="T">Type du résultat attendu par la TaskCompletionSource</typeparam>
		/// <param name="self">Source à linker avec la task</param>
		/// <param name="task">Task qui déclenchera cette source lorsqu'elle sera termiéne</param>
		/// <returns>Task interne de la completionSource, qui sera déclenchée en même temps que la tâche parente</returns>
		public static Task<T> Bind<T>(this TaskCompletionSource<T> self, Task<T> task)
		{
			Contract.NotNull(task);

			if (task.IsCompleted)
			{ // La tâche est déjà terminée !
				PropagateResult(self, task);
			}
			else
			{ // On se branche sur le task pour signaler le TCS quand elle est termiéne
				ThenPromise<T>.Bind(self, task);
			}
			return self.Task;
		}

		#endregion

		#region Func<T, R>...

		/// <summary>Décode et exécute un callback compacté dans un Tuple&lt;Func&lt;...&gt;, ...&gt;</summary>
		/// <typeparam name="R">Type du résultat retourné par le callback</typeparam>
		/// <typeparam name="T">Type du paramètre du callback</typeparam>
		/// <param name="state">Tuple à décoder</param>
		/// <returns>Résultat de l'exécution du callback</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static R InvokeFromStateTuple<T, R>(object state)
		{
			Contract.Debug.Requires(state is Tuple<Func<T, R>, T>, "Incorrect state tuple type");
			var tuple = (Tuple<Func<T, R>, T>) state;
			return tuple.Item1(tuple.Item2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Tuple<Func<T, R>, T> PackStateTuple<T, R>(Func<T, R> func, T arg)
		{
			return new Tuple<Func<T, R>, T>(func, arg);
		}

		#endregion

		#endregion

		/// <summary>Throw l'exception correspondant à une Task qui a échoué</summary>
		[ContractAnnotation("=> halt")]
		public static void ThrowForNonSuccess(Task task)
		{
			Contract.Debug.Requires(task != null);

			switch (task.Status)
			{
				case TaskStatus.Faulted:
				case TaskStatus.Canceled:
				{
					task.GetAwaiter().GetResult(); // => throws;
					break; // never reached
				}
			}

			throw new InvalidOperationException($"The task has not yet completed ({task.Status}).");
		}

		/// <summary>Throw si la Task ne s'est pas déroulée correctement</summary>
		public static void ThrowIfFailed(this Task task)
		{
			if (task == null)
			{
				// README: Probablement un bug d'une méthode "T Foo()" convertit en "Task<T> FooAsync()" qui retourne 'null' au lieu de 'Task.FromResult<T>(null)' !
				// => Vérifier dans la callstack la méthode qui a créé la task sur laquelle on veut obtenir le résultat
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			}
			else if (task.Status != TaskStatus.RanToCompletion)
			{
				ThrowForNonSuccess(task);
			}
		}

		/// <summary>[DON'T DO THIS!!!!!] Similaire a Task.Wait(), mais unwrap correctement les AggregateException</summary>
		/// <remarks>SERIOUSLY, DON'T! Ca tue complètement les perfs si appelé sur le ThreadPool. Trouver un autre moyen pour ne pas avoir besoin de faire ça. Si vraiment pas d'autre choix, essayer de le faire sur un thread dédié.</remarks>
		[Obsolete("This method is evil, and you should be ashamed of yourself! ಠ_ಠ")]
		public static void AwaitCompletion(this Task task)
		{
#if DEBUG
			if (task == null)
			{
				// README: Probablement un bug d'une méthode "T Foo()" convertit en "Task<T> FooAsync()" qui retourn 'null' au lieu de 'Task.FromResult<T>(null)' !
				// => Vérifier dans la callstack la méthode qui a créé la task sur laquelle on veut attendre la completion
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
			}
#endif
			if (task != null && task.Status != TaskStatus.RanToCompletion)
			{ // il faut attendre
				task.GetAwaiter().GetResult();
			}
		}

		#region Then...

		public static Task<T> FastUnwrap<T>(this Task<Task<T>> task)
		{
			var innerTask = (task.Status == TaskStatus.RanToCompletion) ? task.Result : null;
			return innerTask ?? task.Unwrap();
		}

		/// <summary>Déterminer si la task a réussie et qu'on peut exécuter une continuation immédiatement</summary>
		/// <param name="task">Task</param>
		/// <param name="ct">Token d'annulation (idéalement le même qu'utilisé par la task)</param>
		/// <returns>True si la Task est terminée avec succès, et que le token n'est pas annulé</returns>
		public static bool CanRunInline(Task task, CancellationToken ct)
		{
			return task != null && task.Status == TaskStatus.RanToCompletion && !ct.IsCancellationRequested;
		}

		/// <summary>Attends qu'une tache se termine, retourne une valeur si elle s'est déroulée correctement, ou propage l'exception en cas de problème</summary>
		/// <typeparam name="T">Type de la valeur retournée par la tâche actuelle</typeparam>
		/// <typeparam name="R">Type de la valeur retournée par la nouvelle tâche</typeparam>
		/// <param name="task">Tâche qui va effectuer un traitement sans retourner de valeur (ou une valeur qui est ignorée)</param>
		/// <param name="continuation">Fonction appelée pour générer la valeur à retourner, lors que la tâche sous-jacente s'est terminée sans erreur</param>
		/// <param name="ct"></param>
		/// <returns>Tâche qui retourne 'value' si 'task' se déroule correctement, ou qui propage l'exception de la tache sous-jacente</returns>
		/// <remarks>Equivalent a un ContinueWith(...) qui check les erreurs avant de continuer</remarks>
		public static Task<R> Then<T, R>(this Task<T> task, Func<T, R> continuation, CancellationToken ct = default)
		{
			Contract.Debug.Requires(task != null);
			// note: le compilo ne peut pas mettre en cache les delegate static si la méthode est générique, donc on va passer par une classe statique générique

			if (CanRunInline(task, ct))
				return ThenPromise<T, R>.ContinueInline(task, continuation);

			return ThenPromise<T, R>.Continue(task, continuation, ct);
		}

		public static Task<R> Then<T, R>(this Task<T> task, Func<T, Task<R>> asyncContinuation, CancellationToken ct = default)
		{
			Contract.Debug.Requires(task != null);
			// note: le compilo ne peut pas mettre en cache les delegate static si la méthode est générique, donc on va passer par une classe statique générique

			if (CanRunInline(task, ct))
				return ThenPromise<T, R>.ContinueInlineAsync(task, asyncContinuation);

			return ThenPromise<T, Task<R>>.Continue(task, asyncContinuation, ct).FastUnwrap();
		}

		#endregion

		#region Catch...

		/// <summary>Fait en sorte que toute exception non gérée soit observée</summary>
		/// <param name="task">Tâche, qui peut potentiellement déclencher une exception</param>
		/// <returns>La même task, mais avec une continuation qui viendra observer toute erreur</returns>
		/// <remarks>Cette méthode a pour unique but dans la vie de faire taire les warning du compilateur sur les tasks non awaitées (ou variable non utilisées)</remarks>
		public static void Observed<TTask>(this TTask? task)
			where TTask : Task
		{
			if (task == null) return;

			// A la base en .NET 4.0, le destructeur des task rethrow les errors non observées sur le TP ce qui pouvait killer le process
			// => il faut que quelqu'un "touche" a la propriété "Exception" de la task, pour empêcher cela.
			switch (task.Status)
			{
				case TaskStatus.Faulted:
				case TaskStatus.Canceled:
					TouchFaultedTask(task);
					return;

				case TaskStatus.RanToCompletion:
					return;

				default:
					task.ContinueWith((t) => TouchFaultedTask(t), TaskContinuationOptions.OnlyOnFaulted);
					return;
			}
		}

		private static void TouchFaultedTask(Task t)
		{
			// ReSharper disable once UnusedVariable
			var error = t.Exception;
#if DEBUG
			if (t.IsFaulted)
			{
				// C'est une mauvaise pratique, donc râle quand même dans les logs en mode debug!
				System.Diagnostics.Debug.WriteLine($"### muted unobserved failed Task[{t.Id}]: [{error?.InnerException?.GetType().Name}] {error?.InnerException?.Message}");
			}
#endif
		}

		#endregion

		#region Nested Types...

		/// <summary>Helper classe pour permettre au compilateur de mettre en cache les delegate static</summary>
		[DebuggerNonUserCode]
		private static class ThenPromise<T0>
		{

			public static void Bind(TaskCompletionSource<T0> self, Task<T0> task)
			{
				task.ContinueWith((t, state) =>
				{
					PropagateStatus((TaskCompletionSource<T0>) state, t);
				},
					self,
					CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously,
					TaskScheduler.Current
				);
			}

		}

		/// <summary>Helper classe pour permettre au compilateur de mettre en cache les delegate static</summary>
		[DebuggerNonUserCode]
		private static class ThenPromise<T0, TResult>
		{
			public static Task<TResult> Continue(Task<T0> task, Func<T0, TResult> continuation, CancellationToken ct)
			{
				return task.ContinueWith((t, state) =>
				{
					ThrowIfFailed(t);
					return ((Func<T0, TResult>) state)(t.Result);
				}, continuation, ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
			}

			public static Task<TResult> ContinueInline(Task<T0> task, Func<T0, TResult> continuation)
			{
				try
				{
					return Task.FromResult(continuation(task.Result));
				}
				catch (Exception e)
				{
					return Task.FromException<TResult>(e);
				}
			}

			public static Task<TResult> ContinueInlineAsync(Task<T0> task, Func<T0, Task<TResult>> asyncContinuation)
			{
				try
				{
					return asyncContinuation(task.Result);
				}
				catch (Exception e)
				{
					return Task.FromException<TResult>(e);
				}
			}

		}

		[DebuggerNonUserCode]
		private static class RunPromise<T0, T1>
		{

			public static Task<T1> Run(Func<T0, T1> function, T0 arg0, CancellationToken ct, TaskScheduler? taskScheduler)
			{
				return Task.Factory.StartNew(
					(_) => InvokeFromStateTuple<T0, T1>(_),
					PackStateTuple<T0, T1>(function, arg0),
					ct,
					DefaultTaskCreationOptions,
					taskScheduler ?? TaskScheduler.Default
				);
			}

		}

		#endregion

	}

}
