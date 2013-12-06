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

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Helper methods to work on tasks</summary>
	internal static class TaskHelpers
	{

		/// <summary>Return a task that is already completed</summary>
		// README: There is a Task.CompletedTask object in the BCL that is internal, and one 'easy' way to get access to it is via Task.Delay(0) that returns it if param is equal to 0...
		public static readonly Task CompletedTask = Task.Delay(0);

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

			return new Task<T>(() => default(T), cancellationToken);
		}

	}
}
