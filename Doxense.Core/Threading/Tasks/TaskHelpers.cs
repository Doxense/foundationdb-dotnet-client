#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Threading.Tasks
{
	/// <summary>Helper methods to work on tasks</summary>
	public static class TaskHelpers
	{

		#region .NET 4.5 async emulator

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
				Debug.WriteLine($"### muted unobserved failed Task[{t.Id}]: [{error?.InnerException?.GetType().Name}] {error?.InnerException?.Message}");
			}
#endif
		}

		#endregion

	}

}
