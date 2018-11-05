#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace Doxense.Diagnostics.Contracts
{
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Reflection;
	using System.Runtime.CompilerServices;

	[DebuggerNonUserCode]
	internal static class ThrowHelper
	{

		#region ArgumentNullException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentNullException([InvokerParameterName] string paramName)
		{
			return new ArgumentNullException(paramName);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentNullException([InvokerParameterName] string paramName, [NotNull] string message)
		{
			return new ArgumentNullException(paramName, message);
		}

		#endregion

		#region ArgumentException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string message = null)
		{
			// oui, c'est inversé :)
			return new ArgumentException(message, paramName);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string message, object arg0)
		{
			// oui, c'est inversé :)
			return new ArgumentException(string.Format(message, arg0), paramName);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string message, object arg0, object arg1)
		{
			// oui, c'est inversé :)
			return new ArgumentException(string.Format(message, arg0, arg1), paramName);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentException([InvokerParameterName] string paramName, string message, params object[] args)
		{
			// oui, c'est inversé :)
			return new ArgumentException(string.Format(message, args), paramName);
		}

		#endregion

		#region ArgumentOutOfRangeException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentOutOfRangeException([InvokerParameterName] string paramName, object actualValue, string message = null)
		{
			return new ArgumentOutOfRangeException(paramName, actualValue, message);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception ArgumentOutOfRangeException([InvokerParameterName, NotNull] string paramName)
		{
			return new ArgumentOutOfRangeException(paramName);
		}

		#endregion

		#region ObjectDisposedException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(TDisposed disposed)
		{
			return new ObjectDisposedException(disposed.GetType().Name);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(Type type)
		{
			return new ObjectDisposedException(type.Name);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(Type type, string message)
		{
			return new ObjectDisposedException(type.Name, message);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(TDisposed disposed, string message)
		{
			return new ObjectDisposedException(disposed.GetType().Name, message);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(string message)
		{
			return new ObjectDisposedException(typeof(TDisposed).Name, message);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(string message, object arg0)
		{
			return new ObjectDisposedException(typeof(TDisposed).Name, string.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(string message, params object[] args)
		{
			return new ObjectDisposedException(typeof(TDisposed).Name, string.Format(CultureInfo.InvariantCulture, message, args));
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(string message, Exception innnerException)
		{
			return new ObjectDisposedException(message, innnerException);
		}

		#endregion

		#region InvalidOperationException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message)
		{
			return new InvalidOperationException(message);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message, object arg0)
		{
			return new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message, object arg0, object arg1)
		{
			return new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, message, arg0, arg1));
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message, params object[] args)
		{
			return new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, message, args));
		}

		[ContractAnnotation("=> halt")]
		public static void ThrowInvalidOperationException(string message)
		{
			throw InvalidOperationException(message);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
		public static void ThrowInvalidOperationException(string message, object arg0)
		{
			throw InvalidOperationException(message, arg0);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
		public static void ThrowInvalidOperationException(string message, object arg0, object arg1)
		{
			throw InvalidOperationException(message, arg0, arg1);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
		public static void ThrowInvalidOperationException(string message, object arg0, object arg1, object arg2)
		{
			throw InvalidOperationException(message, arg0, arg1, arg2);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message")]
		public static void ThrowInvalidOperationException(string message, params object[] args)
		{
			throw InvalidOperationException(message, args);
		}

		[ContractAnnotation("=> halt"), StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static T ThrowInvalidOperationException<T>(string message)
		{
			throw InvalidOperationException(message);
		}

		#endregion

		#region FormatException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message)
		{
			return new FormatException(message);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message, object arg0)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message, object arg0, object arg1)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, message, arg0, arg1));
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message, params object[] args)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, message, args));
		}

		#endregion

		#region OperationCanceledException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(string message)
		{
			return new OperationCanceledException(message);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(string message, object arg0)
		{
			return new OperationCanceledException(String.Format(CultureInfo.InvariantCulture, message, arg0));
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(string message, params object[] args)
		{
			return new OperationCanceledException(String.Format(CultureInfo.InvariantCulture, message, args));
		}

		#endregion

		#region NotSupportedException...

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedException(string message)
		{
			return new NotSupportedException(message);
		}

		[Pure, NotNull, StringFormatMethod("message"), MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedException(string message, params object[] args)
		{
			return new NotSupportedException(String.Format(CultureInfo.InvariantCulture, message, args));
		}

		#endregion

		[CanBeNull, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception TryMapToKnownException(Type exceptionType, string message, string paramName)
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

		[CanBeNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception TryMapToComplexException(Type exceptionType, string message, string paramName)
		{
			ConstructorInfo constructor;

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

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationNoElements()
		{
			return new InvalidOperationException("Sequence contains no elements.");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationNoMatchingElements()
		{
			return new InvalidOperationException("Sequence contains no matching element.");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static IndexOutOfRangeException IndexOutOfRangeException()
		{
			return new IndexOutOfRangeException("Index was out of range. Must be non-negative and less than the size of the collection.");
		}

		[ContractAnnotation("=> halt")]
		public static void ThrowIndexOutOfRangeException()
		{
			throw IndexOutOfRangeException();
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeIndex(int index)
		{
			// ArgumentOutOfRange_NeedNonNegNum
			// ReSharper disable once UseNameofExpression
			return new ArgumentOutOfRangeException("index", index, "Index was out of range. Must be non-negative and less than the size of the collection.");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeNeedNonNegNum([InvokerParameterName] string paramName)
		{
			// ArgumentOutOfRange_NeedNonNegNum
			return new ArgumentOutOfRangeException(paramName, "Non-negative number required");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentException ArgumentInvalidOffLen()
		{
			// Argument_InvalidOffLen
			return new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedReadOnlyCollection()
		{
			// NotSupported_ReadOnlyCollection
			return new NotSupportedException("Collection is read-only.");
		}

		#endregion

	}

}

#endif
