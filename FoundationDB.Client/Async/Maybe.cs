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
	using System;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Either has a value, nothing, or an exception</summary>
	/// <typeparam name="T">Type of the value</typeparam>
	public struct Maybe<T> : IEquatable<Maybe<T>>, IEquatable<T>
	{

		/// <summary>If true, there is a value. If false, either no value or an exception</summary>
		public readonly bool HasValue;

		/// <summary>If HasValue is true, holds the value. Else, contains default(T)</summary>
		public readonly T Value;

		/// <summary>If HasValue is false optinally holds an error that was captured</summary>
		private readonly object m_errorContainer; // either an Exception, or an ExceptionDispatchInfo

		internal Maybe(bool hasValue, T value, object errorContainer)
		{
#if NET_4_0
			Contract.Requires(errorContainer == null || (errorContainer is Exception));
#else
			Contract.Requires(errorContainer == null || (errorContainer is Exception) || (errorContainer is ExceptionDispatchInfo));
#endif

			this.HasValue = hasValue;
			this.Value = value;
			m_errorContainer = errorContainer;
		}

		/// <summary>Returns an empty value</summary>
		public static Maybe<T> Empty { get { return default(Maybe<T>); } }

		/// <summary>Cached completed Task that always return an empty value</summary>
		public static readonly Task<Maybe<T>> EmptyTask = Task.FromResult(default(Maybe<T>));

		/// <summary>Returns the stored value, of the default value for the type if it was empty</summary>
		/// <returns></returns>
		public T GetValueOrDefault()
		{
			ThrowForNonSuccess();
			return this.Value;
		}

		/// <summary>If true, then there is no value and no error</summary>
		public bool IsEmpty { get { return !this.HasValue && m_errorContainer == null; } }

		/// <summary>If true then there was an error captured</summary>
		public bool HasFailed { get { return m_errorContainer != null; } }

		/// <summary>Return the captured Error, or null if there wasn't any</summary>
		public Exception Error
		{
			get
			{
#if !NET_4_0
				var edi = m_errorContainer as ExceptionDispatchInfo;
				if (edi != null) return edi.SourceException;
#endif
				return m_errorContainer as Exception;
			}
		}

#if !NET_4_0
		/// <summary>Return the captured error context, or null if there wasn't any</summary>
		public ExceptionDispatchInfo CapturedError
		{
			get
			{
				var exception = m_errorContainer as Exception;
				if (exception != null) return ExceptionDispatchInfo.Capture(exception);

				var edi = m_errorContainer as ExceptionDispatchInfo;
				if (edi != null) return edi;

				return null;
			}
		}
#endif

		/// <summary>Rethrows any captured error, if there was one.</summary>
		public void ThrowForNonSuccess()
		{
			if (m_errorContainer != null)
			{
				var exception = m_errorContainer as Exception;
#if !NET_4_0
				if (exception == null)
				{
					((ExceptionDispatchInfo)m_errorContainer).Throw();
					return; // never reached, but helps with code analysis
				}
#endif
				throw exception;
			}
		}

		internal object ErrorContainer
		{
			get { return m_errorContainer; }
		}

		public override bool Equals(object obj)
		{
			System.Diagnostics.Trace.WriteLine("Maybe[" + this + "].Equals(object " + obj + ")");
			if (obj == null) return IsEmpty;
			if (obj is Maybe<T>) return Equals((Maybe<T>)obj);
			if (obj is T) return Equals((T)obj);
			return false;
		}

		public bool Equals(Maybe<T> other)
		{
			System.Diagnostics.Trace.WriteLine("Maybe[" + this + "].Equals(Maybe " + other + ")");
			return this.HasValue == other.HasValue && object.ReferenceEquals(this.ErrorContainer, other.ErrorContainer) && EqualityComparer<T>.Default.Equals(this.Value, other.Value);
		}

		public bool Equals(T other)
		{
			System.Diagnostics.Trace.WriteLine("Maybe[" + this + "].Equals(T " + other + ")");
			return this.HasValue && this.ErrorContainer == null && EqualityComparer<T>.Default.Equals(this.Value, other);
		}

		public override int GetHashCode()
		{
			if (this.ErrorContainer != null) return this.ErrorContainer.GetHashCode();
			if (!this.HasValue) return 0;
			return EqualityComparer<T>.Default.GetHashCode(this.Value);
		}

		public static bool operator ==(Maybe<T> left, Maybe<T> right)
		{
			return left.Equals(right);
		}

		public static bool operator ==(Maybe<T> left, T right)
		{
			return left.Equals(right);
		}

		public static bool operator ==(Nullable<Maybe<T>> left, Nullable<Maybe<T>> right)
		{
			if (!right.HasValue) return !left.HasValue || !left.Value.HasValue;
			if (!left.HasValue) return !right.Value.HasValue;
			return left.Value.Equals(right.Value);
		}

		public static bool operator !=(Maybe<T> left, Maybe<T> right)
		{
			return !left.Equals(right);
		}

		public static bool operator !=(Maybe<T> left, T right)
		{
			return !left.Equals(right);
		}

		public static bool operator !=(Nullable<Maybe<T>> left, Nullable<Maybe<T>> right)
		{
			if (!right.HasValue) return left.HasValue && left.Value.HasValue;
			if (!left.HasValue) return right.Value.HasValue;
			return !left.Value.Equals(right.Value);
		}

		public override string ToString()
		{
			if (this.ErrorContainer != null) return "<error>";
			if (!this.HasValue) return "<empty>";
			if (this.Value == null) return "<null>";
			//TODO: consider adding '['/']' around the value, to distinguish a Maybe<T> between a T in the console and the debugger ?
			return this.Value.ToString();
		}
	}

	/// <summary>
	/// Helper methods for creating <see cref="Maybe{T}"/> instances
	/// </summary>
	public static class Maybe
	{

		/// <summary>Wraps a value into a <see cref="Maybe{T}"/></summary>
		public static Maybe<T> Return<T>(T value)
		{
			return new Maybe<T>(true, value, null);
		}

		/// <summary>Returns an empty <see cref="Maybe{T}"/></summary>
		public static Maybe<T> Nothing<T>()
		{
			return Maybe<T>.Empty;
		}

		/// <summary>Capture an exception into a <see cref="Maybe{T}"/></summary>
		public static Maybe<T> Error<T>(Exception e)
		{
			return new Maybe<T>(false, default(T), e);
		}

#if !NET_4_0
		/// <summary>Capture an exception into a <see cref="Maybe{T}"/></summary>
		public static Maybe<T> Error<T>(ExceptionDispatchInfo e)
		{
			return new Maybe<T>(false, default(T), e);
		}
#endif

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		public static Maybe<R> Apply<T, R>(T value, Func<T, R> lambda)
		{
			Contract.Requires(lambda != null);
			try
			{
				return Return<R>(lambda(value));
			}
			catch (Exception e)
			{
#if NET_4_0
				return Error<R>(e);
#else
				return Error<R>(ExceptionDispatchInfo.Capture(e));
#endif
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		public static Maybe<R> Apply<T, R>(T value, [NotNull] Func<T, Maybe<R>> lambda)
		{
			Contract.Requires(lambda != null);
			try
			{
				return lambda(value);
			}
			catch (Exception e)
			{
#if NET_4_0
				return Error<R>(e);
#else
				return Error<R>(ExceptionDispatchInfo.Capture(e));
#endif
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		public static Maybe<R> Apply<T, R>(Maybe<T> value, [NotNull] Func<T, R> lambda)
		{
			Contract.Requires(lambda != null);
			if (!value.HasValue)
			{
				if (value.HasFailed)
				{
					// keep the original error untouched
					return new Maybe<R>(false, default(R), value.ErrorContainer);
				}
				return Nothing<R>();
			}
			try
			{
				return Return<R>(lambda(value.Value));
			}
			catch (Exception e)
			{
				return Error<R>(e);
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		public static Maybe<R> Apply<T, R>(Maybe<T> value, [NotNull] Func<T, Maybe<R>> lambda)
		{
			Contract.Requires(lambda != null);
			if (!value.HasValue)
			{
				if (value.HasFailed)
				{
					// keep the original error untouched
					return new Maybe<R>(false, default(R), value.ErrorContainer);
				}
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

		/// <summary>Convert a completed <see cref="Task{T}"/> into an equivalent <see cref="Maybe{T}"/></summary>
		public static Maybe<T> FromTask<T>([NotNull] Task<T> task)
		{
			//REVIEW: should we return Maybe<T>.Empty if task == null ?
			Contract.Requires(task != null);
			switch (task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return Return<T>(task.Result);
				}
				case TaskStatus.Faulted:
				{
					//TODO: pass the failed task itself as the error container? (we would keep the original callstack that way...)
					var aggEx = task.Exception.Flatten();
					if (aggEx.InnerExceptions.Count == 1)
					{
						return Maybe.Error<T>(aggEx.InnerException);
					}
					return Maybe.Error<T>(aggEx);
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

		/// <summary>Convert a completed <see cref="Task{T}"/> into an equivalent <see cref="Maybe{T}"/></summary>
		public static Maybe<T> FromTask<T>([NotNull] Task<Maybe<T>> task)
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
					//TODO: pass the failed task itself as the error container? (we would keep the original callstack that way...)
					var aggEx = task.Exception.Flatten();
					if (aggEx.InnerExceptions.Count == 1)
					{
						return Maybe.Error<T>(aggEx.InnerException);
					}
					return Maybe.Error<T>(aggEx);
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

		/// <summary>Streamline a potentially failed Task&lt;Maybe&lt;T&gt;&gt; into a version that capture the error into the <see cref="Maybe{T}"/> itself</summary>
		public static Task<Maybe<T>> Unwrap<T>([NotNull] Task<Maybe<T>> task)
		{
			Contract.Requires(task != null);
			switch(task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return task;
				}
				case TaskStatus.Faulted:
				{
					//TODO: pass the failed task itself as the error container? (we would keep the original callstack that way...)
					var aggEx = task.Exception.Flatten();
					if (aggEx.InnerExceptions.Count == 1)
					{
						return Task.FromResult(Maybe.Error<T>(aggEx.InnerException));
					}
					return Task.FromResult(Maybe.Error<T>(aggEx));
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
