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

namespace FoundationDB.Async
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading.Tasks;

	/// <summary>Either has a value, nothing, or an exception</summary>
	/// <typeparam name="T"></typeparam>
	public struct Maybe<T>
	{

		/// <summary>If true, there is a value. If false, either no value or an exception</summary>
		public readonly bool HasValue;

		/// <summary>If HasValue is true, holds the value. Else, contains default(T)</summary>
		public readonly T Value;

		/// <summary>If HasValue is false optinally holds an error that was captured</summary>
		public readonly ExceptionDispatchInfo Error;

		public Maybe(T value)
		{
			this.HasValue = true;
			this.Value = value;
			this.Error = null;
		}

		public Maybe(ExceptionDispatchInfo error)
		{
			this.HasValue = false;
			this.Value = default(T);
			this.Error = error;
		}

		public static Maybe<T> Empty { get { return default(Maybe<T>); } }

		public static readonly Task<Maybe<T>> EmptyTask = Task.FromResult(default(Maybe<T>));

		public T GetValueOrDefault()
		{
			ThrowIfFailed();
			return this.Value;
		}

		/// <summary>If true, then there is no value and no error</summary>
		public bool IsEmpty { get { return !this.HasValue && this.Error == null; } }

		/// <summary>If true then there was an error captured</summary>
		public bool HasFailed { get { return this.Error != null; } }

		/// <summary>Rethrows any captured error, if there was one.</summary>
		public void ThrowIfFailed()
		{
			if (this.Error != null)
			{
				this.Error.Throw();
			}
		}
	}

	public static class Maybe
	{
		public static Maybe<T> Return<T>(T value)
		{
			return new Maybe<T>(value);
		}

		public static Maybe<T> Nothing<T>()
		{
			return Maybe<T>.Empty;
		}

		public static Maybe<T> Error<T>(Exception e)
		{
			return new Maybe<T>(ExceptionDispatchInfo.Capture(e));
		}

		public static Maybe<T> Error<T>(ExceptionDispatchInfo e)
		{
			return new Maybe<T>(e);
		}

		public static Maybe<R> Apply<T, R>(T value, Func<T, R> lambda)
		{
			try
			{
				return new Maybe<R>(lambda(value));
			}
			catch (Exception e)
			{
				return Error<R>(e);
			}
		}

		public static Maybe<R> Apply<T, R>(T value, Func<T, Maybe<R>> lambda)
		{
			try
			{
				return lambda(value);
			}
			catch (Exception e)
			{
				return Error<R>(e);
			}
		}

		public static Maybe<R> Apply<T, R>(Maybe<T> value, Func<T, R> lambda)
		{
			if (!value.HasValue)
			{
				if (value.HasFailed) return Error<R>(value.Error);
				return Nothing<R>();
			}
			try
			{
				return new Maybe<R>(lambda(value.Value));
			}
			catch (Exception e)
			{
				return Error<R>(e);
			}
		}

		public static Maybe<R> Apply<T, R>(Maybe<T> value, Func<T, Maybe<R>> lambda)
		{
			if (!value.HasValue)
			{
				if (value.HasFailed) return Error<R>(value.Error);
				return Nothing<R>();
			}
			try
			{
				return lambda(value.Value);
			}
			catch (Exception e)
			{
				return Error<R>(e);
			}
		}

		public static Maybe<T> FromTask<T>(Task<T> task)
		{
			Contract.Requires(task != null);
			switch (task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return new Maybe<T>(task.Result);
				}
				case TaskStatus.Faulted:
				{
					return Maybe.Error<T>(task.Exception);
				}
				case TaskStatus.Canceled:
				{
					return Maybe.Error<T>(new OperationCanceledException());
				}
				default:
				{
					throw new InvalidOperationException("Task must be in the completed state");
				}
			}
		}

		public static Maybe<T> FromTask<T>(Task<Maybe<T>> task)
		{
			Contract.Requires(task != null);
			switch (task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return task.Result;
				}
				case TaskStatus.Faulted:
				{
					return Error<T>(task.Exception);
				}
				case TaskStatus.Canceled:
				{
					return Error<T>(new OperationCanceledException());
				}
				default:
				{
					throw new InvalidOperationException("Task must be in the completed state");
				}
			}
		}

		public static Task<Maybe<T>> Unwrap<T>(Task<Maybe<T>> task)
		{
			switch(task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return task;
				}
				case TaskStatus.Faulted:
				{
					return Task.FromResult(Error<T>(task.Exception));
				}
				case TaskStatus.Canceled:
				{
					return Task.FromResult(Error<T>(new OperationCanceledException()));
				}
				default:
				{
					throw new InvalidOperationException("Task must be in the completed state");
				}
			}
		}

	}

}
