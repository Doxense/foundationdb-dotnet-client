#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#nullable enable

namespace Doxense.Diagnostics.Contracts
{
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Reflection;
	using System.Runtime.CompilerServices;

	[DebuggerNonUserCode, PublicAPI]
	public static class ThrowHelper
	{

		#region ArgumentNullException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentNullException([InvokerParameterName] string paramName)
		{
			return new ArgumentNullException(paramName);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentNullException([InvokerParameterName] string paramName, string message)
		{
			return new ArgumentNullException(paramName, message);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentNullException([InvokerParameterName] string paramName)
		{
			throw ArgumentNullException(paramName);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentNullException([InvokerParameterName] string paramName, string message)
		{
			throw ArgumentNullException(paramName, message);
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static T ThrowArgumentNullException<T>([InvokerParameterName] string paramName, string? message = null)
		{
			throw message != null ? new ArgumentNullException(paramName, message) : new ArgumentNullException(paramName);
		}

		#endregion

		#region ArgumentException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string? message = null)
		{
			// oui, c'est inversé :)
			return new ArgumentException(message, paramName);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string message, object? arg0)
		{
			// oui, c'est inversé :)
			return new ArgumentException(string.Format(message, arg0), paramName);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string message, object? arg0, object? arg1)
		{
			// oui, c'est inversé :)
			return new ArgumentException(string.Format(message, arg0, arg1), paramName);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string message, params object[] args)
		{
			// oui, c'est inversé :)
			return new ArgumentException(string.Format(message, args), paramName);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentException([InvokerParameterName] string paramName, string? message = null)
		{
			// oui, c'est inversé :)
			throw ArgumentException(paramName, message);
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static T ThrowArgumentException<T>([InvokerParameterName] string paramName, string? message = null)
		{
			// oui, c'est inversé :)
			throw ArgumentException(paramName, message);
		}

		#endregion

		#region ArgumentOutOfRangeException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentOutOfRangeException([InvokerParameterName] string paramName, object? actualValue, string? message = null)
		{
			return new ArgumentOutOfRangeException(paramName, actualValue, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentOutOfRangeException([InvokerParameterName] string paramName)
		{
			return new ArgumentOutOfRangeException(paramName);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentOutOfRangeException()
		{
			// ReSharper disable once NotResolvedInText
			throw ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName)
		{
			throw ArgumentOutOfRangeException(paramName);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName, string message)
		{
			throw ArgumentOutOfRangeException(paramName, message);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName, object? actualValue, string message)
		{
			throw ArgumentOutOfRangeException(paramName, actualValue, message);
		}

		#endregion

		#region ObjectDisposedException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(TDisposed disposed)
		{
			return new ObjectDisposedException(disposed?.GetType().Name);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(Type type)
		{
			return new ObjectDisposedException(type.Name);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(Type type, string message)
		{
			return new ObjectDisposedException(type.Name, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(TDisposed disposed, string message)
		{
			return new ObjectDisposedException((disposed?.GetType() ?? typeof(TDisposed)).Name, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(string message)
		{
			return new ObjectDisposedException(typeof(TDisposed).Name, message);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(string message, object? arg0)
		{
			return new ObjectDisposedException(typeof(TDisposed).Name, string.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(string message, params object?[] args)
		{
			return new ObjectDisposedException(typeof(TDisposed).Name, string.Format(CultureInfo.InvariantCulture, message, args));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(string message, Exception? innerException)
		{
			return new ObjectDisposedException(message, innerException);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowObjectDisposedException(Type type)
		{
			throw ObjectDisposedException(type);
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowObjectDisposedException(string message, Exception innerException)
		{
			throw ObjectDisposedException(message, innerException);
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)] //fix .NET < 4.5.2
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif

		public static void ThrowObjectDisposedException<TDisposed>(TDisposed disposed)
			where TDisposed : IDisposable
		{
			throw ObjectDisposedException(disposed.GetType());
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)] //fix .NET < 4.5.2
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowObjectDisposedException<TDisposed>(TDisposed disposed, string message)
			where TDisposed : IDisposable
		{
			throw ObjectDisposedException(disposed.GetType(), message);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)] //fix .NET < 4.5.2
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowObjectDisposedException<TDisposed>(TDisposed disposed, string message, object? arg0)
			where TDisposed : IDisposable
		{
			throw ObjectDisposedException(disposed.GetType(), string.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)] //fix .NET < 4.5.2
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowObjectDisposedException<TDisposed>(TDisposed disposed, string message, params object?[] args)
			where TDisposed : IDisposable
		{
			throw ObjectDisposedException(disposed.GetType(), string.Format(CultureInfo.InvariantCulture, message, args));
		}

		#endregion

		#region InvalidOperationException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message)
		{
			return new InvalidOperationException(message);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message, object? arg0)
		{
			return new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message, object? arg0, object? arg1)
		{
			return new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, message, arg0, arg1));
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message, params object?[] args)
		{
			return new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, message, args));
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowInvalidOperationException(string message)
		{
			throw InvalidOperationException(message);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowInvalidOperationException(string message, object? arg0)
		{
			throw InvalidOperationException(message, arg0);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowInvalidOperationException(string message, object? arg0, object? arg1)
		{
			throw InvalidOperationException(message, arg0, arg1);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowInvalidOperationException(string message, object? arg0, object? arg1, object? arg2)
		{
			throw InvalidOperationException(message, arg0, arg1, arg2);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowInvalidOperationException(string message, params object?[] args)
		{
			throw InvalidOperationException(message, args);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static T ThrowInvalidOperationException<T>(string message)
		{
			throw InvalidOperationException(message);
		}

		#endregion

		#region FormatException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message)
		{
			return new FormatException(message);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message, object? arg0)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message, object? arg0, object? arg1)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, message, arg0, arg1));
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message, params object?[] args)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, message, args));
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowFormatException(string message)
		{
			throw FormatException(message);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowFormatException(string message, object? arg0)
		{
			throw FormatException(message, arg0);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowFormatException(string message, object? arg0, object? arg1)
		{
			throw FormatException(message, arg0, arg1);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowFormatException(string message, object? arg0, object? arg1, object? arg2)
		{
			throw FormatException(message, arg0, arg1, arg2);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowFormatException(string message, params object?[] args)
		{
			throw FormatException(message, args);
		}

		#endregion

		#region OperationCanceledException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(string message)
		{
			return new OperationCanceledException(message);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(string message, object? arg0)
		{
			return new OperationCanceledException(String.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(string message, params object?[] args)
		{
			return new OperationCanceledException(String.Format(CultureInfo.InvariantCulture, message, args));
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowOperationCanceledException(string message)
		{
			throw OperationCanceledException(message);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowOperationCanceledException(string message, object? arg0)
		{
			throw OperationCanceledException(message, arg0);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowOperationCanceledException(string message, params object?[] args)
		{
			throw OperationCanceledException(message, args);
		}

		#endregion

		#region NotSupportedException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedException(string message)
		{
			return new NotSupportedException(message);
		}

		[Pure, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedException(string message, params object?[] args)
		{
			return new NotSupportedException(String.Format(CultureInfo.InvariantCulture, message, args));
		}

		#endregion

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception? TryMapToKnownException(Type exceptionType, string message, string? paramName)
		{
			// d'abord on regarde si c'est un type "simple"
			if (exceptionType == typeof(ArgumentNullException))
			{
				return new ArgumentNullException(paramName, message);
			}
			if (exceptionType == typeof(InvalidOperationException))
			{
				return new InvalidOperationException(message);
			}
			if (exceptionType == typeof(ArgumentException))
			{
				return new ArgumentException(message, paramName);
			}
			if (exceptionType == typeof(ArgumentOutOfRangeException))
			{
				return new ArgumentOutOfRangeException(paramName, message);
			}
			if (exceptionType == typeof(ObjectDisposedException))
			{
				return new ObjectDisposedException(paramName, message);
			}
			if (exceptionType == typeof (FormatException))
			{
				return new FormatException(message);
			}
			return null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception? TryMapToComplexException(Type exceptionType, string message, string? paramName)
		{
			ConstructorInfo? constructor;

			if (paramName != null)
			{ // essayes de trouver un constructeur qui prenne deux string dont une soit "paramName"
				constructor = exceptionType.GetConstructor(new[] { typeof(string), typeof(string) });
				if (constructor != null)
				{
					if (constructor.GetParameters()[0].Name == "paramName")
					{
						return constructor.Invoke(new object[] { paramName, message }) as Exception;
					}
					else if (constructor.GetParameters()[1].Name == "paramName")
					{
						return constructor.Invoke(new object[] { message, paramName }) as Exception;
					}
				}
			}

			// essayes de trouver un constructeur qui prenne une string
			constructor = exceptionType.GetConstructor(new[] { typeof(string) });
			if (constructor != null)
			{
				return constructor.Invoke(new object[] { message }) as Exception;
			}

			// c'est un type d'erreur qui ne prend pas de params ?
			constructor = exceptionType.GetConstructor(Type.EmptyTypes);
			if (constructor != null)
			{
				return constructor.Invoke(null) as Exception;
			}

			return null;
		}

		#region Collection Errors...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationNoElements()
		{
			return new InvalidOperationException("Sequence contains no elements.");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationNoMatchingElements()
		{
			return new InvalidOperationException("Sequence contains no matching element.");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static IndexOutOfRangeException IndexOutOfRangeException()
		{
			return new IndexOutOfRangeException("Index was out of range. Must be non-negative and less than the size of the collection.");
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowIndexOutOfRangeException()
		{
			throw IndexOutOfRangeException();
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static T ThrowIndexOutOfRangeException<T>()
		{
			throw IndexOutOfRangeException();
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeIndex(int index)
		{
			// ArgumentOutOfRange_NeedNonNegNum
			// ReSharper disable once UseNameofExpression
			return new ArgumentOutOfRangeException("index", index, "Index was out of range. Must be non-negative and less than the size of the collection.");
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowArgumentOutOfRangeIndex(int index)
		{
			// ArgumentOutOfRange_NeedNonNegNum
			throw ArgumentOutOfRangeIndex(index);
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static T ThrowArgumentOutOfRangeIndex<T>(int index)
		{
			throw IndexOutOfRangeException();
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeNeedNonNegNum([InvokerParameterName] string paramName)
		{
			// ArgumentOutOfRange_NeedNonNegNum
			return new ArgumentOutOfRangeException(paramName, "Non-negative number required");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentException ArgumentInvalidOffLen()
		{
			// Argument_InvalidOffLen
			return new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedReadOnlyCollection()
		{
			// NotSupported_ReadOnlyCollection
			return new NotSupportedException("Collection is read-only.");
		}

		[ContractAnnotation("=> halt")]
#if USE_ANNOTATIONS
		[System.Diagnostics.CodeAnalysis.DoesNotReturn]
#endif
		public static void ThrowNotSupportedReadOnlyCollection()
		{
			// NotSupported_ReadOnlyCollection
			throw NotSupportedReadOnlyCollection();
		}

		#endregion

	}

}
