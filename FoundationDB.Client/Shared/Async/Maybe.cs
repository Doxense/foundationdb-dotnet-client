#region BSD License
/* Copyright (c) 2013-2023 Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense //REVIEW: what would be the best namespace for this? (mostly used by layers)
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Represents the result of a deferred computation</summary>
	/// <typeparam name="T">Type of the values produced by the computation</typeparam>
	/// <remarks>A computation can be in three states: 1) hasn't run yet and has no value, 2) has run and produced a value, 3) has run and faulted.</remarks>
	[PublicAPI]
	public readonly struct Maybe<T> : IEquatable<Maybe<T>>, IEquatable<T>, IComparable<Maybe<T>>, IComparable<T>, IFormattable
	{
		/// <summary>Returns an empty result (no computation)</summary>
		public static readonly Maybe<T> Nothing = default;

		/// <summary>Returns a result that is the default of type <typeparamref name="T"/> (0, false, null, ...)</summary>
		public static readonly Maybe<T> Default = new Maybe<T>(default);

		/// <summary>Cached completed Task that always return an empty result</summary>
		public static readonly Task<Maybe<T>> EmptyTask = Task.FromResult(default(Maybe<T>));

		#region Private Fields...

		// ==================================================================================
		//  m_hasValue |   m_value  |  m_error     | description
		// ==================================================================================
		//     True    |   Result   |   null       | The computation produced a result (that could be the default of the type, but is not "empty")
		//     False   |      -     |   null       | The computation did not produce any result
		//     False   |      -     |   Exception  | The computation has failed

		/// <summary>If true, there is a value. If false, either no value or an exception</summary>
		[AllowNull]
		private readonly T m_value;

		/// <summary>If HasValue is true, holds the value. Else, contains default(T)</summary>
		private readonly bool m_hasValue;

		/// <summary>If HasValue is false optionally holds an error that was captured</summary>
		private readonly object? m_errorContainer; // either an Exception, or an ExceptionDispatchInfo

		#endregion

		public Maybe([AllowNull] T value)
		{
			m_hasValue = true;
			m_value = value;
			m_errorContainer = null;
		}

		internal Maybe(bool hasValue, [AllowNull] T value, object? errorContainer)
		{
			Contract.Debug.Requires(errorContainer == null || (errorContainer is Exception) || (errorContainer is ExceptionDispatchInfo));

			m_hasValue = hasValue;
			m_value = value;
			m_errorContainer = errorContainer;
		}

		/// <summary>There is a value</summary>
		/// <remarks>!(IsEmpty || HasFailed)</remarks>
		public bool HasValue
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_hasValue;
		}

		/// <summary>Returns the value if the computation succeeded</summary>
		/// <exception cref="InvalidOperationException">If the value is empty</exception>
		/// <exception cref="AggregateException">If the value has failed to compute</exception>
		public T Value
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_hasValue ? m_value : ThrowInvalidState();
		}

		/// <summary>Returns the value if the computation succeeded, or default(<typeparamref name="T"/>) in all other cases</summary>
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T GetValueOrDefault()
		{
			return m_value;
		}

		public Exception? Error
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			[Pure]
			get => m_errorContainer is ExceptionDispatchInfo edi
				? edi.SourceException
				: m_errorContainer as Exception;
		}

		/// <summary>Return the captured error context, or null if there wasn't any</summary>
		public ExceptionDispatchInfo? CapturedError => m_errorContainer is Exception exception ? ExceptionDispatchInfo.Capture(exception) : m_errorContainer as ExceptionDispatchInfo;

		/// <summary>The value failed to compute</summary>
		/// <returns>!(HasValue || IsEmpty)</returns>
		public bool Failed
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_errorContainer != null;
		}

		/// <summary>Rethrows any captured error, if there was one.</summary>
		public void ThrowForNonSuccess()
		{
			if (m_errorContainer != null)
			{
				if (!(m_errorContainer is Exception exception))
				{
					((ExceptionDispatchInfo) m_errorContainer).Throw();
					return; // never reached, but helps with code analysis
				}
				throw exception;
			}
		}

		internal object? ErrorContainer => m_errorContainer;

		/// <summary>No value was returned</summary>
		/// <remarks>!(HasValue || Failed)</remarks>
		public bool IsEmpty => !m_hasValue && m_errorContainer == null;

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		private T ThrowInvalidState()
		{
			if (m_errorContainer != null) throw new AggregateException("A computation has triggered an exception.", this.Error);
			if (!m_hasValue) throw new InvalidOperationException("This computation has no value.");
			throw new InvalidOperationException("This computation already has a value.");
		}

		[Pure]
		public static Func<Maybe<T>, Maybe<TResult>> Return<TResult>(Func<T, TResult> computation)
		{
			return Bind(x => new Maybe<TResult>(computation(x)));
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Failure(Exception error)
		{
			return new Maybe<T>(false, default, error);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Failure(ExceptionDispatchInfo error)
		{
			return new Maybe<T>(false, default, error);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator T(Maybe<T> m)
		{
			return m.Value;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Maybe<T>(T value)
		{
			return new Maybe<T>(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Maybe<T>(Exception error)
		{
			return Failure(error);
		}

		public bool Equals(Maybe<T> other)
		{
			if (m_hasValue) return other.m_hasValue && EqualityComparer<T>.Default.Equals(m_value, other.m_value);
			if (m_errorContainer != null) return !m_hasValue && m_errorContainer.Equals(other.m_errorContainer);
			return !other.m_hasValue & other.m_errorContainer == null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(T other)
		{
			return m_hasValue && EqualityComparer<T>.Default.Equals(m_value, other);
		}

		public override bool Equals(object obj)
		{
			if (obj == null) return !m_hasValue;
			if (obj is T value) return Equals(value);
			if (obj is Maybe<T> maybe) return Equals(maybe);
			if (obj is Exception err) return !m_hasValue && err.Equals(m_errorContainer);
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			return m_hasValue ? EqualityComparer<T>.Default.GetHashCode(m_value) : m_errorContainer?.GetHashCode() ?? -1;
		}

		public int CompareTo(Maybe<T> other)
		{
			// in order: "nothing", then values, then errors

			if (m_hasValue)
			{ // Some
				if (other.m_hasValue) return Comparer<T>.Default.Compare(m_value, other.m_value);
				if (other.m_errorContainer != null) return -1; // values come before errors
				return +1; // values come after nothing
			}

			if (m_errorContainer != null)
			{ // Error
				if (other.m_hasValue || other.m_errorContainer == null) return +1; // errors come after everything except errors
				//note: this is tricky, because we cannot really sort Exceptions, so this sort may not be stable :(
				// => the "only" way would be to compare their hash codes!
				return ReferenceEquals(m_errorContainer, other.m_errorContainer) ? 0 : m_errorContainer.GetHashCode().CompareTo(other.m_errorContainer.GetHashCode());
			}

			// Nothing comes before everything except nothing
			return other.m_hasValue || other.m_errorContainer != null ? -1 : 0;
		}

		public int CompareTo(T other)
		{
			// in order: "nothing", then values, then errors
			if (!m_hasValue)
			{
				return m_errorContainer != null ? +1 : -1;
			}
			return Comparer<T>.Default.Compare(m_value, other);
		}

		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			if (this.Failed) return "<error>";
			if (!this.HasValue) return "<none>";
			if (this.Value == null) return "<null>"; //REVIEW: => "<nothing>" ?
			if (this.Value is IFormattable fmt) return fmt.ToString(format, formatProvider);
			return this.Value.ToString();
		}

		public override string ToString()
		{
			return ToString(null, null);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Maybe<T> left, T right)
		{
			return left.Equals(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Maybe<T> left, T right)
		{
			return !left.Equals(right);
		}

		public static bool operator >(Maybe<T> left, T right)
		{
			return left.CompareTo(right) > 0;
		}

		public static bool operator >=(Maybe<T> left, T right)
		{
			return left.CompareTo(right) >= 0;
		}

		public static bool operator <(Maybe<T> left, T right)
		{
			return left.CompareTo(right) < 0;
		}

		public static bool operator <=(Maybe<T> left, T right)
		{
			return left.CompareTo(right) <= 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Maybe<T> left, Maybe<T> right)
		{
			return left.Equals(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Maybe<T> left, Maybe<T> right)
		{
			return !left.Equals(right);
		}

		public static bool operator >(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) > 0;
		}

		public static bool operator >=(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) >= 0;
		}

		public static bool operator <(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) < 0;
		}

		public static bool operator <=(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) <= 0;
		}

		#region Function Binding...

		public static Func<Maybe<T>, Maybe<TResult>> Bind<TResult>(Func<T, Maybe<TResult>> computation)
		{
			return (x) =>
			{
				if (x.m_errorContainer != null) return new Maybe<TResult>(false, default, x.m_errorContainer);
				if (!x.m_hasValue) return Maybe<TResult>.Nothing;

				try
				{
					return computation(x.m_value);
				}
				catch (Exception e)
				{
					return Maybe<TResult>.Failure(e);
				}
			};
		}

		public static Func<Maybe<T>, Maybe<T>, Maybe<TResult>> Bind<TResult>(Func<T, T, Maybe<TResult>> computation)
		{
			return (x, y) =>
			{
				if (x.m_errorContainer != null || y.m_errorContainer != null) return Maybe.Error(default(TResult), x.Error, y.Error);
				if (x.m_hasValue && y.m_hasValue)
				{
					try
					{

						return computation(x.m_value, y.m_value);
					}
					catch (Exception e)
					{
						return Maybe<TResult>.Failure(e);
					}
				}
				return Maybe<TResult>.Nothing;
			};
		}

		#endregion

	}

	/// <summary>Helper class to deal with Maybe&lt;T&gt; monads</summary>
	[PublicAPI]
	public static class Maybe
	{

		/// <summary>Create a <see cref="Maybe{T}">Maybe&lt;T&gt;</see> from a concrete value</summary>
		/// <typeparam name="T">Value type</typeparam>
		/// <param name="value">Result of a computation</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Return<T>(T value)
		{
			// ENTER THE MONAD !
			return new Maybe<T>(value);
		}

		/// <summary>Converts this value into a <see cref="Maybe{T}">Maybe&lt;T&gt;</see> instance.</summary>
		/// <typeparam name="T">Value type</typeparam>
		/// <remarks>Note: if <typeparamref name="T"/> is a ReferenceType and the current <paramref name="value"/> is null, a resolved Maybe instance will be returned. You need to call <see cref="ToMaybeOrNothing{T}"/> if you want to return <see cref="Maybe{T}.Nothing"/> instead for this case!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> ToMaybe<T>(this T value)
		{
			// ENTER THE MONAD !
			return new Maybe<T>(value);
		}

		/// <summary>Converts this value into <see cref="Maybe{T}">Maybe&lt;T&gt;</see> instance, or <see cref="Maybe{T}.Nothing"/> if it is null.</summary>
		/// <typeparam name="T">Type de la valeur</typeparam>
		/// <remarks>If the current <paramref name="value"/> is <c>null</c>, then <see cref="Maybe{T}.Nothing"/> will be returned.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> ToMaybe<T>(this T? value)
			where T : struct
		{
			return value.HasValue ? new Maybe<T>(value.Value) : Maybe<T>.Nothing;
		}

		/// <summary>Converts this value into a <see cref="Maybe{T}">Maybe&lt;T&gt;</see> instance, or <see cref="Maybe{T}.Nothing"/> if it is null.</summary>
		/// <typeparam name="T">Value type</typeparam>
		/// <remarks>Note: if <typeparamref name="T"/> is a ReferenceType and the current <paramref name="value"/> is null, then <see cref="Maybe{T}"/>.Nothing will be returned. You need to call <see cref="ToMaybe{T}(T)"/> if you want to return <see cref="Maybe{T}.Default"/> instead for this case!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> ToMaybeOrNothing<T>(this T value)
			where T : class
		{
			// ENTER THE MONAD
			return value == null ? Maybe<T>.Nothing : new Maybe<T>(value);
		}

		/// <summary>Returns <see cref="Maybe{T}.Nothing"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Nothing<T>()
		{
			// ENTER THE MONAD !
			return default;
		}

		/// <summary>Returns <see cref="Maybe{T}.Nothing"/></summary>
		/// <param name="_">Value that is only used to help the compiler detect the type</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Nothing<T>(T _)
		{
			// ENTER THE MONAD !
			return default;
		}

		/// <summary>Returns a <see cref="Maybe{T}"/> that represents a faulted computation</summary>
		/// <param name="error">Exception that occured during the computation</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(Exception error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Returns a <see cref="Maybe{T}"/> that represents a faulted computation</summary>
		/// <param name="error">Captured exception that occured during the computation</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(ExceptionDispatchInfo error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Returns a <see cref="Maybe{T}"/> that represents a faulted computation</summary>
		/// <param name="_">Value that is only used to help the compiler detect the type</param>
		/// <param name="error">Exception that occured during the computation</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(T _, Exception error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Returns a <see cref="Maybe{T}"/> that represents a faulted computation</summary>
		/// <param name="_">Value that is only used to help the compiler detect the type</param>
		/// <param name="error">Captured exception that occured during the computation</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(T _, ExceptionDispatchInfo error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Returns a <see cref="Maybe{T}"/> that represents multiple faulted computations</summary>
		/// <param name="_">Value that is only used to help the compiler detect the type</param>
		/// <param name="error0">First exception (can be null)</param>
		/// <param name="error1">Second exception (can be null)</param>
		[Pure]
		public static Maybe<T> Error<T>([AllowNull] T _, Exception? error0, Exception? error1)
		{
			// At least one of them must be non-null
			Contract.Debug.Assert(error0 != null || error1 != null);

			if (error1 == null)
			{
				Contract.Debug.Assert(error0 != null);
				return Maybe<T>.Failure(error0);
			}
			if (error0 == null)
			{
				return Maybe<T>.Failure(error1);
			}
			return Maybe<T>.Failure(new AggregateException(error0, error1));
		}

		/// <summary>Returns the equivalent <see cref="Nullable{T}"/> from the value of this instance</summary>
		/// <returns>The result of the computation, or <c>default(T?)</c> it did not happen or has faulted</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? ToNullable<T>(this Maybe<T> m)
			where T : struct
		{
			// EXIT THE MONAD
			//TODO: throw if faulted ?
			return m.HasValue ? m.Value : default(T?);
		}

		/// <summary>Converts this value into <see cref="Maybe{T}">Maybe&lt;T&gt;</see> instance, or <see cref="Maybe{T}.Nothing"/> if it is null.</summary>
		/// <typeparam name="T">Type de la valeur</typeparam>
		/// <remarks>If the current <paramref name="value"/> is <c>null</c>, then <see cref="Maybe{T}.Nothing"/> will be returned.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> FromNullable<T>(T? value)
			where T : struct
		{
			return value.HasValue ? new Maybe<T>(value.Value) : default;
		}

		/// <summary>Return the result of this instance, or a default value if it is <see cref="Maybe{T}.Nothing"/> or is faulted</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T OrDefault<T>(this Maybe<T> m, T @default = default)
		{
			// EXIT THE MONAD
			return m.HasValue ? m.Value : @default;
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(T value, [InstantHandle] Func<T, TResult> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			try
			{
				return Return<TResult>(lambda(value));
			}
			catch (Exception e)
			{
				return Error<TResult>(ExceptionDispatchInfo.Capture(e));
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(T value, [InstantHandle] Func<T, Maybe<TResult>> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			try
			{
				return lambda(value);
			}
			catch (Exception e)
			{
				return Error<TResult>(ExceptionDispatchInfo.Capture(e));
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(Maybe<T> value, [InstantHandle] Func<T, TResult> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			if (!value.HasValue)
			{
				if (value.Failed)
				{
					// keep the original error untouched
					return new Maybe<TResult>(false, default, value.ErrorContainer);
				}
				return Nothing<TResult>();
			}
			try
			{
				return Return<TResult>(lambda(value.Value));
			}
			catch (Exception e)
			{
				return Error<TResult>(e);
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(Maybe<T> value, [InstantHandle] Func<T, Maybe<TResult>> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			if (!value.HasValue)
			{
				if (value.Failed)
				{
					// keep the original error untouched
					return new Maybe<TResult>(false, default, value.ErrorContainer);
				}
				return Nothing<TResult>();
			}
			try
			{
				return lambda(value.Value);
			}
			catch (Exception e)
			{
				return Error<TResult>(e);
			}
		}

		/// <summary>Convert a completed <see cref="Task{T}"/> into an equivalent <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<T> FromTask<T>(Task<T> task)
		{
			//REVIEW: should we return Maybe<T>.Empty if task == null ?
			Contract.Debug.Requires(task != null);
			switch (task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return Return<T>(task.Result);
				}
				case TaskStatus.Faulted:
				{
					//TODO: pass the failed task itself as the error container? (we would keep the original callstack that way...)
					var aggEx = task.Exception!.Flatten();
					if (aggEx.InnerExceptions.Count == 1)
					{
						return Error<T>(aggEx.InnerException);
					}
					return Error<T>(aggEx);
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

		/// <summary>Convert a completed <see cref="Task{T}"/> with <typeparamref name="T"/> being a <see cref="Maybe{T}"/>, into an equivalent <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<T> FromTask<T>(Task<Maybe<T>> task)
		{
			Contract.Debug.Requires(task != null);
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
						return Error<T>(aggEx.InnerException);
					}
					return Error<T>(aggEx);
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
		[Pure]
		public static Task<Maybe<T>> Unwrap<T>(Task<Maybe<T>> task)
		{
			Contract.Debug.Requires(task != null);
			switch (task.Status)
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

		[Pure]
		private static Func<Maybe<TInput>, Maybe<TResult>> Combine<TInput, TIntermediate, TResult>(Func<Maybe<TInput>, Maybe<TIntermediate>> f, Func<Maybe<TIntermediate>, Maybe<TResult>> g)
		{
			return (mt) => g(f(mt));
		}

		/// <summary>Combine two lambdas into a single one</summary>
		/// <typeparam name="TInput">Type of input of <paramref name="f"/></typeparam>
		/// <typeparam name="TIntermediate">Type of the output of <paramref name="f"/> and input of <paramref name="g"/></typeparam>
		/// <typeparam name="TResult">Type of the result of <paramref name="g"/></typeparam>
		/// <param name="f">First function (that runs first)</param>
		/// <param name="g">Second function (that runs on the result of <paramref name="f"/></param>
		/// <returns>Function h(x) = g(f(x))</returns>
		[Pure]
		public static Func<Maybe<TInput>, Maybe<TResult>> Bind<TInput, TIntermediate, TResult>(Func<TInput, Maybe<TIntermediate>> f, Func<TIntermediate, Maybe<TResult>> g)
		{
			return Combine(Maybe<TInput>.Bind(f), Maybe<TIntermediate>.Bind<TResult>(g));
		}

		/// <summary>Combine two lambdas into a single one</summary>
		/// <typeparam name="TInput">Type of input of <paramref name="f"/></typeparam>
		/// <typeparam name="TIntermediate">Type of the output of <paramref name="f"/> and input of <paramref name="g"/></typeparam>
		/// <typeparam name="TResult">Type of the result of <paramref name="g"/></typeparam>
		/// <param name="f">First function (that runs first)</param>
		/// <param name="g">Second function (that runs on the result of <paramref name="f"/></param>
		/// <returns>Function h(x) = g(f(x))</returns>
		[Pure]
		public static Func<Maybe<TInput>, Maybe<TResult>> Bind<TInput, TIntermediate, TResult>(Func<TInput, Maybe<TIntermediate>> f, Func<Maybe<TIntermediate>, Maybe<TResult>> g)
		{
			return Combine(Maybe<TInput>.Bind(f), g);
		}

	}

}

#endif
