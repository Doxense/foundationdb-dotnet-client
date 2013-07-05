using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Client.Utils
{
	/// <summary>Helper methods to work on tasks</summary>
	internal static class TaskHelpers
	{

		/// <summary>Helper type cache class</summary>
		public static class Cache<T>
		{

			public static readonly Task<T> Default = Task.FromResult<T>(default(T));

			/// <summary>Returns a lambda function that returns the default value of <typeparam name="T"/></summary>
			public static Func<T> Nop
			{
				get
				{
					//note: the compiler should but this in a static cache variable
					return () => default(T);
				}
			}

			/// <summary>Returns the identity function for <typeparam name="T"/></summary>
			public static Func<T, T> Identity
			{
				get
				{
					//note: the compiler should but this in a static cache variable
					return (x) => x;
				}
			}

		}

		/// <summary>Return a task that is already completed</summary>
		// README: There is a Task.CompletedTask object in the BCL that is internal, and one 'easy' way to get access to it is via Task.Delay(0) that returns it if param is equal to 0...
		public static readonly Task CompletedTask = Task.Delay(0);

		/// <summary>Already completed task that returns false</summary>
		public static readonly Task<bool> FalseTask = Task.FromResult<bool>(false);

		/// <summary>Already completed task that returns true</summary>
		public static readonly Task<bool> TrueTask = Task.FromResult<bool>(true);

		/// <summary>Returns an already completed boolean task that is either true of false</summary>
		/// <param name="value">Value of the task</param>
		/// <returns>Already completed task the returns <paramref name="value"/></returns>
		public static Task<bool> FromResult(bool value)
		{
			return value ? TrueTask : FalseTask;
		}

		/// <summary>Returns a cached completed task that returns the default value of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <returns>Task that is already completed, and returns default(<typeparamref name="T"/>)</returns>
		public static Task<T> Default<T>()
		{
			return Cache<T>.Default;
		}

		/// <summary>Runs a synchronous lambda inline, exposing it as if it was task</summary>
		/// <typeparam name="T1">Type of the result of the lambda</typeparam>
		/// <param name="lambda">Synchronous lambda function that returns a value, or throws exceptions</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that either contains the result of the lambda, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCancelledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="lambda"/> is null</exception>
		public static Task<R> Inline<R>(Func<R> lambda, CancellationToken ct = default(CancellationToken))
		{
			if (lambda == null) throw new ArgumentNullException("lambda");

			if (ct.IsCancellationRequested) return FromCancellation<R>(ct);
			try
			{
				var res = lambda();
				return Task.FromResult(res);
			}
			catch (Exception e)
			{
				return FromFailure<R>(e, ct);
			}
		}

		/// <summary>Runs a synchronous action inline, exposing it as if it was task</summary>
		/// <typeparam name="T1">Type of the parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCancelledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1>(Action<T1> action, T1 arg1, CancellationToken ct = default(CancellationToken))
		{
			// note: if action is null, then there is a bug in the caller, and it should blow up instantly (will help preserving the call stack)
			if (action == null) throw new ArgumentNullException("action");
			// for all other exceptions, they will be wrapped in the returned task
			if (ct.IsCancellationRequested) return FromCancellation<object>(ct);
			try
			{
				action(arg1);
				return TaskHelpers.CompletedTask;
			}
			catch (Exception e)
			{
				return FromFailure<object>(e, ct);
			}
		}

		/// <summary>Runs a synchronous action inline, exposing it as if it was task</summary>
		/// <typeparam name="T1">Type of the parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCancelledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
		{
			// note: if action is null, then there is a bug in the caller, and it should blow up instantly (will help preserving the call stack)
			if (action == null) throw new ArgumentNullException("action");
			// for all other exceptions, they will be wrapped in the returned task
			if (ct.IsCancellationRequested) return FromCancellation<object>(ct);
			try
			{
				action(arg1, arg2);
				return TaskHelpers.CompletedTask;
			}
			catch (Exception e)
			{
				return FromFailure<object>(e, ct);
			}
		}

		/// <summary>Runs a synchronous action inline, exposing it as if it was task</summary>
		/// <typeparam name="T1">Type of the parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCancelledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2, T3>(Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
		{
			// note: if action is null, then there is a bug in the caller, and it should blow up instantly (will help preserving the call stack)
			if (action == null) throw new ArgumentNullException("action");
			// for all other exceptions, they will be wrapped in the returned task
			if (ct.IsCancellationRequested) return FromCancellation<object>(ct);
			try
			{
				action(arg1, arg2, arg3);
				return TaskHelpers.CompletedTask;
			}
			catch (Exception e)
			{
				return FromFailure<object>(e, ct);
			}
		}

		/// <summary>Runs a synchronous action inline, exposing it as if it was task</summary>
		/// <typeparam name="T1">Type of the parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCancelledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
		{
			// note: if action is null, then there is a bug in the caller, and it should blow up instantly (will help preserving the call stack)
			if (action == null) throw new ArgumentNullException("action");
			// for all other exceptions, they will be wrapped in the returned task
			if (ct.IsCancellationRequested) return FromCancellation<object>(ct);
			try
			{
				action(arg1, arg2, arg3, arg4);
				return TaskHelpers.CompletedTask;
			}
			catch (Exception e)
			{
				return FromFailure<object>(e, ct);
			}
		}

		/// <summary>Runs a synchronous action inline, exposing it as if it was task</summary>
		/// <typeparam name="T1">Type of the parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCancelledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, CancellationToken ct = default(CancellationToken))
		{
			// note: if action is null, then there is a bug in the caller, and it should blow up instantly (will help preserving the call stack)
			if (action == null) throw new ArgumentNullException("action");
			// for all other exceptions, they will be wrapped in the returned task
			if (ct.IsCancellationRequested) return FromCancellation<object>(ct);
			try
			{
				action(arg1, arg2, arg3, arg4, arg5);
				return TaskHelpers.CompletedTask;
			}
			catch (Exception e)
			{
				return FromFailure<object>(e, ct);
			}
		}

		/// <summary>Wraps a classic lambda into one that supports cancellation</summary>
		/// <param name="lambda">Lambda that does not support cancellation</param>
		/// <returns>New lambda that will check if the token is cancelled before calling <paramref name="lambda"/></returns>
		public static Func<TSource, CancellationToken, TResult> WithCancellation<TSource, TResult>(Func<TSource, TResult> lambda)
		{
			Contract.Requires(lambda != null);
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
			Contract.Requires(lambda != null);
			return (value, ct) =>
			{
				if (ct.IsCancellationRequested) return FromCancellation<TResult>(ct);
				return lambda(value);
			};
		}

		/// <summary>Returns a cancelled Task that is linked with a cancelled token</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="cancellationToken">Cancellation token that should already be cancelled</param>
		/// <returns>Task in the Cancelled state that is linked with this cancellation token</returns>
		public static Task<T> FromCancellation<T>(CancellationToken cancellationToken)
		{
			// There is a Task.FromCancellation<T>() method in the BCL, but unfortunately it is internal :(
			// The "best" way I've seen to emulate the same behavior, is creating a fake task (with a dummy action) with the same alread-cancelled CancellationToken
			// This should throw the correct TaskCancelledException that is linked with this token

			// ensure that it is actually cancelled, so that we don't deadlock
			if (!cancellationToken.IsCancellationRequested) throw new InvalidOperationException();

			return new Task<T>(Cache<T>.Nop, cancellationToken);
		}

		/// <summary>Returns a cancelled Task that is not linked to any particular token</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <returns>Task in the Cancelled state</returns>
		public static Task<T> Cancelled<T>()
		{
			var tcs = new TaskCompletionSource<T>();
			tcs.TrySetCanceled();
			return tcs.Task;
		}

		/// <summary>Returns a failed Task that wraps an exception</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="e">Exception that will be wrapped in the task</param>
		/// <returns>Task that is already completed, and that will rethrow the exception once observed</returns>
		public static Task<T> FromException<T>(Exception e)
		{
			// There is a Task.FromException<T>() method in the BCL, but unfortunately it is internal :(
			// We can only emulate it by calling TrySetException on a dummy TaskCompletionSource
			// Also, we should flattent AggregateException so as not to create huge chain of aggEx

			var tcs = new TaskCompletionSource<T>();

			var aggEx = e as AggregateException;
			if (aggEx == null)
				tcs.TrySetException(e);
			else
				tcs.TrySetException(aggEx.InnerExceptions);

			//note: also, to avoid blowing up the process if nobody observes the task, we observe it once
			var _ = tcs.Task.Exception;

			return tcs.Task;
		}

		public static Task<T> FromFailure<T>(Exception e, CancellationToken cancellationToken)
		{
			if (e is OperationCanceledException)
			{
				if (cancellationToken.IsCancellationRequested)
					return FromCancellation<T>(cancellationToken);
				else
					return Cancelled<T>();
			}

			return FromException<T>(e);
		}

		/// <summary>Safely cancel and dispose a CancellationTokenSource</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled and disposed</param>
		public static void SafeCancelAndDispose(this CancellationTokenSource source)
		{
			if (source != null)
			{
				try
				{
					source.Cancel();
				}
				catch (ObjectDisposedException) { }
				finally
				{
					source.Dispose();
				}
			}
		}

		/// <summary>Safely cancel and dispose a CancellationTokenSource, executing the registered callbacks on the thread pool</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled and disposed</param>
		public static void SafeCancelAndDisposeDefered(this CancellationTokenSource source)
		{
			if (source != null)
			{
				ThreadPool.QueueUserWorkItem((state) => { SafeCancelAndDispose((CancellationTokenSource)state); }, source);
			}
		}

	}
}
