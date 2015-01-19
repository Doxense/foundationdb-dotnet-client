#region BSD Licence
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

namespace FoundationDB.Async
{
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Helper methods to work on tasks</summary>
	internal static class TaskHelpers
	{

		/// <summary>Helper type cache class</summary>
		public static class Cache<T>
		{

			public static readonly Task<T> Default = Task.FromResult<T>(default(T));

			/// <summary>Returns a lambda function that returns the default value of <typeparamref name="T"/></summary>
			public static Func<T> Nop
			{
				get
				{
					//note: the compiler should but this in a static cache variable
					return () => default(T);
				}
			}

			/// <summary>Returns the identity function for <typeparamref name="T"/></summary>
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

		/// <summary>Continue processing a task, if it succeeded</summary>
		public static async Task Then<T>(this Task<T> task, [NotNull] Action<T> inlineContinuation)
		{
			// Note: we use 'await' instead of ContinueWith, so that we can give the caller a nicer callstack in case of errors (instead of an AggregateExecption)

			var value = await task.ConfigureAwait(false);
			inlineContinuation(value);
		}

		/// <summary>Continue processing a task, if it succeeded</summary>
		public static async Task<R> Then<T, R>(this Task<T> task, [NotNull] Func<T, R> inlineContinuation)
		{
			// Note: we use 'await' instead of ContinueWith, so that we can give the caller a nicer callstack in case of errors (instead of an AggregateExecption)

			var value = await task.ConfigureAwait(false);
			return inlineContinuation(value);
		}

		/// <summary>Runs a synchronous lambda inline, exposing it as if it was task</summary>
		/// <typeparam name="R">Type of the result of the lambda</typeparam>
		/// <param name="lambda">Synchronous lambda function that returns a value, or throws exceptions</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that either contains the result of the lambda, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCanceledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="lambda"/> is null</exception>
		public static Task<R> Inline<R>([NotNull] Func<R> lambda, CancellationToken ct = default(CancellationToken))
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
		/// <param name="arg1">Argument that will be passed to <paramref name="action"/></param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCanceledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1>([NotNull] Action<T1> action, T1 arg1, CancellationToken ct = default(CancellationToken))
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
		/// <typeparam name="T1">Type of the first parameter of the lambda</typeparam>
		/// <typeparam name="T2">Type of the second parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="arg1">First argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg2">Second argument that will be passed to <paramref name="action"/></param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCanceledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2>([NotNull] Action<T1, T2> action, T1 arg1, T2 arg2, CancellationToken ct = default(CancellationToken))
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
		/// <typeparam name="T1">Type of the first parameter of the lambda</typeparam>
		/// <typeparam name="T2">Type of the second parameter of the lambda</typeparam>
		/// <typeparam name="T3">Type of the third parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="arg1">First argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg2">Second argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg3">Third argument that will be passed to <paramref name="action"/></param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCanceledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2, T3>([NotNull] Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3, CancellationToken ct = default(CancellationToken))
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
		/// <typeparam name="T1">Type of the first parameter of the lambda</typeparam>
		/// <typeparam name="T2">Type of the second parameter of the lambda</typeparam>
		/// <typeparam name="T3">Type of the third parameter of the lambda</typeparam>
		/// <typeparam name="T4">Type of the fourth parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="arg1">First argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg2">Second argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg3">Third argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg4">Fourth argument that will be passed to <paramref name="action"/></param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCanceledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2, T3, T4>([NotNull] Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken ct = default(CancellationToken))
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
		/// <typeparam name="T1">Type of the first parameter of the lambda</typeparam>
		/// <typeparam name="T2">Type of the second parameter of the lambda</typeparam>
		/// <typeparam name="T3">Type of the third parameter of the lambda</typeparam>
		/// <typeparam name="T4">Type of the fourth parameter of the lambda</typeparam>
		/// <typeparam name="T5">Type of the fifth parameter of the lambda</typeparam>
		/// <param name="action">Synchronous action that takes a value.</param>
		/// <param name="arg1">First argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg2">Second argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg3">Third argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg4">Fourth argument that will be passed to <paramref name="action"/></param>
		/// <param name="arg5">Fifth argument that will be passed to <paramref name="action"/></param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Task that is either already completed, wraps the exception that was thrown, or is in the cancelled state if the cancellation token fired or if the task throwed an OperationCanceledException</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="action"/> is null</exception>
		public static Task Inline<T1, T2, T3, T4, T5>([NotNull] Action<T1, T2, T3, T4, T5> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, CancellationToken ct = default(CancellationToken))
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
		public static Func<TSource, CancellationToken, TResult> WithCancellation<TSource, TResult>([NotNull] Func<TSource, TResult> lambda)
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
		public static Func<TSource, CancellationToken, Task<TResult>> WithCancellation<TSource, TResult>([NotNull] Func<TSource, Task<TResult>> lambda)
		{
			Contract.Requires(lambda != null);
			return (value, ct) =>
			{
				if (ct.IsCancellationRequested) return FromCancellation<TResult>(ct);
				return lambda(value);
			};
		}

		/// <summary>Returns a cancelled Task that is linked with a specific token</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="cancellationToken">Cancellation token that should already be cancelled</param>
		/// <returns>Task in the cancelled state that is linked with this cancellation token</returns>
		public static Task<T> FromCancellation<T>(CancellationToken cancellationToken)
		{
			// There is a Task.FromCancellation<T>() method in the BCL, but unfortunately it is internal :(
			// The "best" way I've seen to emulate the same behavior, is creating a fake task (with a dummy action) with the same alread-cancelled CancellationToken
			// This should throw the correct TaskCanceledException that is linked with this token

			// ensure that it is actually cancelled, so that we don't deadlock
			if (!cancellationToken.IsCancellationRequested) throw new InvalidOperationException();

			return new Task<T>(Cache<T>.Nop, cancellationToken);
		}

		/// <summary>Returns a cancelled Task that is not linked to any particular token</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <returns>Task in the cancelled state</returns>
		public static Task<T> Canceled<T>()
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

		/// <summary>Returns a failed Task that wraps an exception</summary>
		/// <typeparam name="T">Type of the result of the task</typeparam>
		/// <param name="e">Exception that will be wrapped in the task</param>
		/// <param name="cancellationToken">Original cancellation token that may have triggered</param>
		/// <returns>Task that is already completed, and that will rethrow the exception once observed</returns>
		public static Task<T> FromFailure<T>(Exception e, CancellationToken cancellationToken)
		{
			if (e is OperationCanceledException)
			{
				if (cancellationToken.IsCancellationRequested)
					return FromCancellation<T>(cancellationToken);
				else
					return Canceled<T>();
			}

			return FromException<T>(e);
		}

		/// <summary>Update the state of a TaskCompletionSource to reflect the type of error that occurred</summary>
		public static void PropagateException<T>([NotNull] TaskCompletionSource<T> tcs, Exception e)
		{
			if (e is OperationCanceledException)
			{
				tcs.TrySetCanceled();
			}
			else if (e is AggregateException)
			{
				tcs.TrySetException(((AggregateException) e).Flatten().InnerExceptions);
			}
			else
			{
				tcs.TrySetException(e);
			}
		}

		/// <summary>Ensure that a task will be observed by someone, in the event that it would fail</summary>
		/// <remarks>This helps discard any unhandled task exceptions, for fire&amp;forget tasks</remarks>
		public static void Observe(Task task)
		{
			if (task != null)
			{
				if (!task.IsCompleted)
				{
					task.ContinueWith((t) => { var _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
				}
				else
				{
					var _ = task.Exception;
				}
			}
		}

		/// <summary>Safely cancel a CancellationTokenSource</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled</param>
		public static void SafeCancel(this CancellationTokenSource source)
		{
			if (source != null && !source.IsCancellationRequested)
			{
				try
				{
					source.Cancel();
				}
				catch (ObjectDisposedException) { }
			}
		}
		/// <summary>Safely cancel and dispose a CancellationTokenSource</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled and disposed</param>
		public static void SafeCancelAndDispose(this CancellationTokenSource source)
		{
			if (source != null)
			{
				try
				{
					if (!source.IsCancellationRequested)
					{
						source.Cancel();
					}
				}
				catch (ObjectDisposedException) { }
				finally
				{
					source.Dispose();
				}
			}
		}

		/// <summary>Safely cancel a CancellationTokenSource, executing the registered callbacks on the thread pool</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled</param>
		public static void SafeCancelDefered(this CancellationTokenSource source)
		{
			if (source != null)
			{
				ThreadPool.QueueUserWorkItem((state) => SafeCancel((CancellationTokenSource)state), source);
			}
		}

		/// <summary>Safely cancel and dispose a CancellationTokenSource, executing the registered callbacks on the thread pool</summary>
		/// <param name="source">CancellationTokenSource that needs to be cancelled and disposed</param>
		public static void SafeCancelAndDisposeDefered(this CancellationTokenSource source)
		{
			if (source != null)
			{
				ThreadPool.QueueUserWorkItem((state) => SafeCancelAndDispose((CancellationTokenSource)state), source);
			}
		}

	}
}
